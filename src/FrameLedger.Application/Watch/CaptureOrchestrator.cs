using FrameLedger.Application.Capture;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.Application.Watch;

/// <summary>
/// The Agent's <c>--serve</c> loop (P2 PR-F): one process snapshot per second, the watcher's events, and
/// <b>one session per game at a time</b> through <see cref="ISessionRecorder"/>. It decides nothing about
/// hooking — the recorder's loop reaches <c>HookedCaptureGate</c> with the row's consent, the kill switch and
/// the guard exactly as a console verb does, and a game with hooking off lands as a Tier-2 row (or is
/// discarded under the recorder's minimum length). What this class owns is <i>when</i> a session starts and
/// that two never run for the same game.
/// </summary>
/// <remarks>
/// <para>
/// <b>The running-session table has one lock, and the sessions themselves never take it.</b> Until P3 PR-1b
/// <see cref="PollOnceAsync"/> was the table's only writer; the pipe's <c>LaunchGame</c> and <c>StopSession</c>
/// now reach it from the pipe's tasks too, so the table is guarded rather than queued (HANDOFF §P3 decision
/// D12 as built: two commands do not justify a channel and a second place a start can wait). Each session
/// runs on its own task and touches nothing here.
/// </para>
/// <para>
/// <b>Attach mode from the watcher, launch mode on request.</b> A watched process is one the user started
/// elsewhere; <see cref="LaunchAsync"/> starts the title itself through the <see cref="ILaunchRecorderFactory"/>
/// the composition root supplies (the console's <c>launch</c> verb builds the same by hand), and the election
/// after a launcher exits is <see cref="ElectAfterLaunchAsync"/>, which both share. A
/// <see cref="TrackedProcessGone"/> starts nothing: the session's own loop sees the exit first.
/// </para>
/// <para>
/// <b>A stop is a token, not a cancellation.</b> <see cref="StopSession"/> cancels the session's own stop
/// token, which the loop reads at its next tick and ends as <see cref="SessionEndReason.StoppedByUser"/> —
/// drained, finalized, stored — where the host's cancellation would leave a <c>.partial</c> for recovery.
/// </para>
/// </remarks>
public sealed class CaptureOrchestrator
{
    private readonly ISessionRecorder _recorder;
    private readonly IGameRepository _games;
    private readonly IProcessSnapshotSource _processes;
    private readonly IExecutableIdentitySource _identity;
    private readonly OrchestratorOptions _options;
    private readonly Action<string> _log;
    private readonly ILaunchRecorderFactory? _launches;
    private readonly ProcessWatcher _watcher = new();
    private readonly Lock _table = new();
    private readonly Dictionary<long, Running> _running = [];

    public CaptureOrchestrator(ISessionRecorder recorder, IGameRepository games, IProcessSnapshotSource processes,
        IExecutableIdentitySource identity, OrchestratorOptions options, Action<string> log, ILaunchRecorderFactory? launches = null)
    {
        _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _launches = launches;
    }

    /// <summary>Sessions started here that have not finished.</summary>
    public int RunningSessions
    {
        get
        {
            lock (_table)
            {
                return _running.Values.Count(static r => !r.Task.IsCompleted);
            }
        }
    }

    /// <summary>Poll until cancelled, then wait for every running session to finalize.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_options.PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await PollOnceAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Stopping: the sessions were started with this token, so their loops end and finalize on their
            // own task; awaiting them here is what gives finalize its grace before the host is gone.
        }

        await DrainRunningAsync().ConfigureAwait(false);
    }

    /// <summary>One tick: snapshot, diff, start what appeared, report what finished.</summary>
    public async Task<IReadOnlyList<WatchEvent>> PollOnceAsync(CancellationToken ct)
    {
        IReadOnlyList<GameRow> watchlist = await _games.ListAsync(ct).ConfigureAwait(false);
        IReadOnlyList<ProcessSnapshot> snapshot = _processes.Take();
        IReadOnlyList<WatchEvent> events = _watcher.Poll(snapshot, watchlist);

        foreach (WatchEvent e in events)
        {
            switch (e)
            {
                case TrackedProcessAppeared appeared:
                    Start(appeared, ct);
                    break;
                case TrackedProcessGone gone:
                    _log($"watch: pid {gone.Pid} ({gone.Game.Name}) exited");
                    break;
                default:
                    break;
            }
        }

        Reap();
        return events;
    }

    /// <summary>
    /// <c>StopSession</c> (P3 PR-1b): ask the session with this guid to end at its next tick. False when no running
    /// session started here carries it — an elected session after a launch, or a console capture, is not addressable.
    /// </summary>
    public bool StopSession(Guid sessionGuid)
    {
        lock (_table)
        {
            foreach (Running r in _running.Values)
            {
                if (r.SessionGuid == sessionGuid && !r.Task.IsCompleted)
                {
                    r.Stop.Cancel();
                    _log($"session {sessionGuid:N}: stop requested; the loop ends at its next tick");
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// <c>LaunchGame</c> (P3 PR-1b): start the game with id <paramref name="gameId"/> in launch mode, under the
    /// one-session-per-game rule, and run the launcher election after it. Consent is still the gate's, one process
    /// later; this method decides only that a session starts.
    /// </summary>
    public async ValueTask<LaunchResult> LaunchAsync(long gameId, string arguments, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (_launches is null)
        {
            return LaunchResult.Of(LaunchOutcome.Unavailable);
        }

        IReadOnlyList<GameRow> rows = await _games.ListAsync(ct).ConfigureAwait(false);
        GameRow? game = rows.FirstOrDefault(g => g.Id == gameId);
        if (game is null)
        {
            return LaunchResult.Of(LaunchOutcome.UnknownGame);
        }

        lock (_table)
        {
            if (_running.TryGetValue(gameId, out Running? current) && !current.Task.IsCompleted)
            {
                return LaunchResult.Of(LaunchOutcome.SessionRunning);
            }

            var running = new Running(Guid.NewGuid());
            running.Task = RunLaunchAsync(game, arguments, running.SessionGuid, running.Stop.Token, ct);
            _running[gameId] = running;
            return new LaunchResult(LaunchOutcome.Accepted, running.SessionGuid);
        }
    }

    /// <summary>
    /// After a launch-mode session ended with the launcher gone (<see cref="SessionEndReason.LaunchTargetExited"/>)
    /// or sitting on its window (<see cref="SessionEndReason.LaunchNoPresentationRuntime"/>): elect the newest
    /// tracked descendant and run an attach-mode session against it. Null when nothing was elected.
    /// </summary>
    public async Task<RecordedSession?> ElectAfterLaunchAsync(RecordedSession launched, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(launched);
        if (launched.Outcome.Reason is not (SessionEndReason.LaunchTargetExited or SessionEndReason.LaunchNoPresentationRuntime)
            || launched.Outcome.TargetPid == 0)
        {
            return null;
        }

        IReadOnlyList<GameRow> watchlist = await _games.ListAsync(ct).ConfigureAwait(false);
        HashSet<string> tracked = new(watchlist.Select(static g => g.Fingerprint.ExePath), StringComparer.OrdinalIgnoreCase);
        ProcessSnapshot? elected = DescendantElection.Elect(_processes.Take(), launched.Outcome.TargetPid, tracked.Contains);
        if (elected is not { ImagePath: { } path })
        {
            _log($"election: no tracked descendant of pid {launched.Outcome.TargetPid} is running; nothing to attach to");
            return null;
        }

        _log($"election: pid {elected.Value.Pid} ({path}) is the newest tracked descendant; attaching");
        return await _recorder.RecordAsync(Attach(path, null, null, default), ct).ConfigureAwait(false);
    }

    private void Start(TrackedProcessAppeared appeared, CancellationToken ct)
    {
        lock (_table)
        {
            if (_running.TryGetValue(appeared.Game.Id, out Running? current) && !current.Task.IsCompleted)
            {
                _log($"watch: pid {appeared.Pid} ({appeared.Game.Name}) appeared while a session for that game is running; one at a time");
                return;
            }

            _log(appeared.StalePath
                ? $"watch: pid {appeared.Pid} matched {appeared.Game.Name} BY FILE NAME ONLY — it runs from {appeared.ImagePath}, not the path on record; the session is keyed on the real path"
                : $"watch: pid {appeared.Pid} ({appeared.Game.Name}) appeared; starting a session");
            var running = new Running(Guid.NewGuid());
            RecordRequest request = Attach(appeared.ImagePath, appeared.Game.Name, running.SessionGuid, running.Stop.Token);
            running.Task = RunAndReportAsync(_recorder, request, ct);
            _running[appeared.Game.Id] = running;
        }
    }

    /// <summary>The session on its own task, reporting its own end; nothing else awaits a stored task.</summary>
    private async Task RunAndReportAsync(ISessionRecorder recorder, RecordRequest request, CancellationToken ct)
    {
        try
        {
            RecordedSession r = await Task.Run(() => recorder.RecordAsync(request, ct), ct).ConfigureAwait(false);
            Report(r);
        }
        catch (OperationCanceledException)
        {
            _log("session: cancelled before it could finalize; the .partial stays for recovery");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log($"session: FAULTED {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Launch mode on its own task: the environment, the session, the election, each reported.</summary>
    private async Task RunLaunchAsync(GameRow game, string arguments, Guid sessionGuid, CancellationToken stop, CancellationToken ct)
    {
        try
        {
            string path = game.Fingerprint.ExePath;
            using ILaunchRecording launch = await _launches!.PrepareAsync(path, ct).ConfigureAwait(false);
            _log($"launch: {game.Name}: {launch.Description}");
            var request = new RecordRequest
            {
                NormalisedExePath = path,
                Observed = _identity.Read(path),
                PayloadPath = _options.PayloadPath,
                Mode = CaptureMode.Launch,
                Arguments = arguments,
                GameName = game.Name,
                SessionGuid = sessionGuid,
                StopToken = stop,
            };
            RecordedSession r = await Task.Run(() => launch.Recorder.RecordAsync(request, ct), ct).ConfigureAwait(false);
            Report(r);

            RecordedSession? elected = await ElectAfterLaunchAsync(r, ct).ConfigureAwait(false);
            if (elected is not null)
            {
                Report(elected);
            }
        }
        catch (OperationCanceledException)
        {
            _log("session: cancelled before it could finalize; the .partial stays for recovery");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log($"session: FAULTED {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Report(RecordedSession r) =>
        _log($"session {r.SessionGuid:N}: {r.Outcome.Reason}; {r.Finalize.Status} (tier {(int)r.Row.Tier}, exit={r.ExitStatus}, frames={r.Row.FrameCount})");

    private RecordRequest Attach(string normalisedExePath, string? gameName, Guid? sessionGuid, CancellationToken stop)
    {
        ExecutableFingerprint? observed = _identity.Read(normalisedExePath);
        return new RecordRequest
        {
            NormalisedExePath = normalisedExePath,
            Observed = observed,
            PayloadPath = _options.PayloadPath,
            Mode = CaptureMode.Attach,
            GameName = gameName,
            SessionGuid = sessionGuid,
            StopToken = stop,
        };
    }

    private void Reap()
    {
        lock (_table)
        {
            foreach ((long gameId, Running running) in _running.ToArray())
            {
                if (running.Task.IsCompleted)
                {
                    _running.Remove(gameId);
                    running.Dispose();
                }
            }
        }
    }

    private async Task DrainRunningAsync()
    {
        Running[] all;
        lock (_table)
        {
            all = [.. _running.Values];
            _running.Clear();
        }

        await Task.WhenAll(all.Select(static r => r.Task)).ConfigureAwait(false);
        foreach (Running r in all)
        {
            r.Dispose();
        }
    }

    /// <summary>One started session: its guid (chosen here, so the stop is addressable), its task, its stop token.</summary>
    private sealed class Running(Guid sessionGuid) : IDisposable
    {
        public Guid SessionGuid { get; } = sessionGuid;

        public CancellationTokenSource Stop { get; } = new();

        public Task Task { get; set; } = System.Threading.Tasks.Task.CompletedTask;

        public void Dispose() => Stop.Dispose();
    }
}

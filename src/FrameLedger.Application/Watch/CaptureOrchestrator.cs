using FrameLedger.Application.Capture;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.Application.Watch;

/// <summary>
/// The Agent's <c>--serve</c> loop (P2 PR-F): one process snapshot per second, the watcher's events, and
/// <b>one session per executable at a time</b> through <see cref="ISessionRecorder"/>. It decides nothing about
/// hooking — the recorder's loop reaches <c>HookedCaptureGate</c> with the row's consent, the kill switch and
/// the guard exactly as a console verb does, and a game with hooking off lands as a Tier-2 row (or is
/// discarded under the recorder's minimum length). What this class owns is <i>when</i> a session starts and
/// that two never run for the same executable.
/// </summary>
/// <remarks>
/// <para>
/// <b>A file name is not an identity (2026-09-23, HANDOFF D25).</b> A process that matches a library entry by its
/// FILE NAME only starts nothing. It may only be that entry's executable on a drive that changed its letter — the
/// relocator's rule — and then the entry moves and the session is the entry's; anything else is not in the library, is
/// said once in the log, and records nothing. Until this date such a process got a session under the matched entry's
/// NAME, keyed on its own path — and the recorder inserted a new entry with that name: *HELLO, HELLO WORLD!*'s
/// <c>swiftshader\Game.exe</c> became a second "Flower in Us" (the only entry named <c>Game.exe</c>), and its NW.js
/// children then matched the new entry exactly and started a second session beside the first.
/// </para>
/// <para>
/// <b>The running table is keyed by the executable's path</b>, not the entry's id (2026-09-23): an entry removed and
/// re-imported while its game runs, or two entries merged, get a new id for the same executable, and a second process of
/// it must still find the first session.
/// </para>
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
    private readonly ExecutableRelocator? _relocator;
    private readonly LatestProcessSnapshot? _latest;
    private readonly ProcessWatcher _watcher = new();
    private readonly Lock _table = new();
    private readonly Dictionary<string, Running> _running = new(StringComparer.OrdinalIgnoreCase);

    // Processes a file-name match declined (pid → the path they run from), touched only by the poll: their exit is
    // not an entry's, and a launch that spawns five of them is said once.
    private readonly Dictionary<int, string> _declined = [];

    public CaptureOrchestrator(ISessionRecorder recorder, IGameRepository games, IProcessSnapshotSource processes,
        IExecutableIdentitySource identity, OrchestratorOptions options, Action<string> log, ILaunchRecorderFactory? launches = null,
        ExecutableRelocator? relocator = null, LatestProcessSnapshot? latest = null)
    {
        _relocator = relocator;
        _latest = latest;
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

    /// <summary>Whether a session started here for the entry <paramref name="gameId"/> is still running — what a merge of two entries waits for.</summary>
    public bool IsRecording(long gameId)
    {
        lock (_table)
        {
            return _running.Values.Any(r => r.GameId == gameId && !r.Task.IsCompleted);
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
        _latest?.Publish(snapshot);
        IReadOnlyList<WatchEvent> events = _watcher.Poll(snapshot, watchlist);

        foreach (WatchEvent e in events)
        {
            switch (e)
            {
                case TrackedProcessAppeared { StalePath: false } appeared:
                    Start(appeared, ct);
                    break;
                case TrackedProcessAppeared nameOnly:
                    if (await AdoptIfMovedAsync(nameOnly, ct).ConfigureAwait(false) is { } adopted)
                    {
                        Start(adopted, ct);
                    }
                    else
                    {
                        Decline(nameOnly);
                    }

                    break;
                case TrackedProcessGone gone:
                    if (!_declined.Remove(gone.Pid))
                    {
                        _log($"watch: pid {gone.Pid} ({gone.Game.Name}) exited");
                    }

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

        string path = game.Fingerprint.ExePath;
        lock (_table)
        {
            if (_running.TryGetValue(path, out Running? current) && !current.Task.IsCompleted)
            {
                return LaunchResult.Of(LaunchOutcome.SessionRunning);
            }

            var running = new Running(Guid.NewGuid(), game.Id);
            running.Task = RunLaunchAsync(game, arguments, running.SessionGuid, running.Stop.Token, ct);
            _running[path] = running;
            return new LaunchResult(LaunchOutcome.Accepted, running.SessionGuid);
        }
    }

    /// <summary>
    /// After a launch-mode session ended with the launcher gone (<see cref="SessionEndReason.LaunchTargetExited"/>)
    /// or sitting on its window (<see cref="SessionEndReason.LaunchNoPresentationRuntime"/>): elect the newest
    /// tracked descendant and run an attach-mode session against it. Null when nothing was elected, or when the watcher
    /// already records that executable — the child appeared while the launcher ran, and one session is its session.
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

        GameRow? row = watchlist.FirstOrDefault(g => string.Equals(g.Fingerprint.ExePath, path, StringComparison.OrdinalIgnoreCase));
        Task<RecordedSession> session;
        lock (_table)
        {
            // Registered under its executable like any session (2026-09-23), so the watcher does not start a second one for
            // it; and when the watcher got there first, that session is the child's. The launch's own entry (the same
            // executable relaunching itself) is the one exception: it is this very flow, still awaiting this election.
            if (_running.TryGetValue(path, out Running? current) && !current.Task.IsCompleted && current.SessionGuid != launched.SessionGuid)
            {
                _log($"election: pid {elected.Value.Pid} ({path}) is already recorded by session {current.SessionGuid:N}; not a second one");
                return null;
            }

            _log($"election: pid {elected.Value.Pid} ({path}) is the newest tracked descendant; attaching");
            if (current is not null && current.SessionGuid == launched.SessionGuid)
            {
                session = _recorder.RecordAsync(Attach(path, row?.Name, null, default), ct);
            }
            else
            {
                var running = new Running(Guid.NewGuid(), row?.Id ?? 0);
                session = _recorder.RecordAsync(Attach(path, row?.Name, running.SessionGuid, running.Stop.Token), ct);
                running.Task = session;
                _running[path] = running;
            }
        }

        return await session.ConfigureAwait(false);
    }

    /// <summary>
    /// A file-name match is only ever the entry's own executable on a drive that changed its letter (2026-09-22, narrowed
    /// 2026-09-23): the relocator moves the entry first, so the session is keyed on the entry's (now real) path and its
    /// consent applies. Null otherwise — the process is not in the library.
    /// </summary>
    private async ValueTask<TrackedProcessAppeared?> AdoptIfMovedAsync(TrackedProcessAppeared appeared, CancellationToken ct)
    {
        if (_relocator is null || !await _relocator.TryAdoptAsync(appeared.Game, appeared.ImagePath, ct).ConfigureAwait(false))
        {
            return null;
        }

        GameRow moved = await _games.FindByIdAsync(appeared.Game.Id, ct).ConfigureAwait(false) ?? appeared.Game;
        return appeared with { Game = moved, StalePath = false };
    }

    /// <summary>A file-name match that is not the entry's executable: said once per executable while it runs, and nothing recorded.</summary>
    private void Decline(TrackedProcessAppeared nameOnly)
    {
        bool said = _declined.ContainsValue(nameOnly.ImagePath);
        _declined[nameOnly.Pid] = nameOnly.ImagePath;
        if (!said)
        {
            _log($"watch: pid {nameOnly.Pid} runs {nameOnly.ImagePath} — its file name matches {nameOnly.Game.Name} ({nameOnly.Game.Fingerprint.ExePath}), "
                 + "but it is not that entry's executable; it is not in the library, so nothing is recorded");
        }
    }

    private void Start(TrackedProcessAppeared appeared, CancellationToken ct)
    {
        lock (_table)
        {
            if (_running.TryGetValue(appeared.ImagePath, out Running? current) && !current.Task.IsCompleted)
            {
                _log($"watch: pid {appeared.Pid} ({appeared.Game.Name}) appeared while a session for that executable is running; one at a time");
                return;
            }

            _log($"watch: pid {appeared.Pid} ({appeared.Game.Name}) appeared; starting a session");
            var running = new Running(Guid.NewGuid(), appeared.Game.Id);
            RecordRequest request = Attach(appeared.ImagePath, appeared.Game.Name, running.SessionGuid, running.Stop.Token);
            running.Task = RunAndReportAsync(_recorder, request, ct);
            _running[appeared.ImagePath] = running;
        }
    }

    /// <summary>The session on its own task, reporting its own end; nothing else awaits a stored task.</summary>
    private async Task RunAndReportAsync(ISessionRecorder recorder, RecordRequest request, CancellationToken ct)
    {
        try
        {
            RecordedSession r = await Task.Run(() => recorder.RecordAsync(request, ct), ct).ConfigureAwait(false);
            Report(r, Name(request));
        }
        catch (OperationCanceledException)
        {
            _log($"session {SessionId(request)}: cancelled before it could finalize; the .partial stays for recovery — {Name(request)}");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log($"session {SessionId(request)}: FAULTED {ex.GetType().Name}: {ex.Message} — {Name(request)} ({request.NormalisedExePath})");
        }
    }

    /// <summary>The game a log line is about (2026-09-23: the owner's log said "FAULTED" twice and not which game or session).</summary>
    private static string Name(RecordRequest request) => request.GameName ?? Path.GetFileName(request.NormalisedExePath);

    private static string SessionId(RecordRequest request) => request.SessionGuid?.ToString("N") ?? "(unnamed)";

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
            Report(r, game.Name);

            RecordedSession? elected = await ElectAfterLaunchAsync(r, ct).ConfigureAwait(false);
            if (elected is not null)
            {
                Report(elected, $"elected after {game.Name}");
            }
        }
        catch (OperationCanceledException)
        {
            _log($"session {sessionGuid:N}: cancelled before it could finalize; the .partial stays for recovery — {game.Name}");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log($"session {sessionGuid:N}: FAULTED {ex.GetType().Name}: {ex.Message} — {game.Name} ({game.Fingerprint.ExePath})");
        }
    }

    private void Report(RecordedSession r, string game) =>
        _log($"session {r.SessionGuid:N}: {r.Outcome.Reason}; {r.Finalize.Status} (tier {(int)r.Row.Tier}, exit={r.ExitStatus}, frames={r.Row.FrameCount}){GuardDetail(r.Outcome)} — {game}");

    /// <summary>
    /// What the guard said, when it said anything but a plain allow (2026-09-21). A SafetyUnhook on the owner's machine
    /// left "SafetyUnhook; Saved" in this log and nothing about WHICH finding fired; the pipe event carried it and the
    /// log is what survives.
    /// </summary>
    private static string GuardDetail(CaptureOutcome o)
    {
        Domain.AntiCheat.AntiCheatVerdict v = o.Verdict;
        if (v.Reason == Domain.AntiCheat.AntiCheatRefusalReason.Allow)
        {
            return string.Empty;
        }

        string text = $"; guard={v.Reason}" + (v.Family.Length > 0 ? "/" + v.Family : string.Empty) + (v.Signal.Length > 0 ? "/" + v.Signal : string.Empty);
        return o.HookingTurnedOff ? text + "; hooking-turned-off" : text;
    }

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
            foreach ((string path, Running running) in _running.ToArray())
            {
                if (running.Task.IsCompleted)
                {
                    _running.Remove(path);
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

    /// <summary>One started session: its guid (chosen here, so the stop is addressable), its entry, its task, its stop token.</summary>
    private sealed class Running(Guid sessionGuid, long gameId) : IDisposable
    {
        public Guid SessionGuid { get; } = sessionGuid;

        /// <summary>The entry the session was started for (0 when an elected executable has none); what <see cref="IsRecording"/> answers from.</summary>
        public long GameId { get; } = gameId;

        public CancellationTokenSource Stop { get; } = new();

        public Task Task { get; set; } = System.Threading.Tasks.Task.CompletedTask;

        public void Dispose() => Stop.Dispose();
    }
}

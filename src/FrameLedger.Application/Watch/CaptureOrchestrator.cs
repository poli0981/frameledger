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
/// <b>Single consumer, by construction.</b> <see cref="PollOnceAsync"/> is the only writer of the running-session
/// table and the only reader of the watcher; the sessions themselves run on their own tasks and touch nothing
/// here. <c>04_CAPTURE</c> §Threading model's watcher row is this method on a <see cref="PeriodicTimer"/>.
/// </para>
/// <para>
/// <b>Attach mode only.</b> A watched process is one the user started elsewhere; launch mode is the console
/// verb's, and the election after a launcher exits is <see cref="ElectAfterLaunchAsync"/>, called by that verb.
/// A <see cref="TrackedProcessGone"/> starts nothing: the session's own loop sees the exit first.
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
    private readonly ProcessWatcher _watcher = new();
    private readonly Dictionary<long, Task> _running = new();

    public CaptureOrchestrator(ISessionRecorder recorder, IGameRepository games, IProcessSnapshotSource processes,
        IExecutableIdentitySource identity, OrchestratorOptions options, Action<string> log)
    {
        _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Sessions started here that have not finished.</summary>
    public int RunningSessions => _running.Values.Count(static t => !t.IsCompleted);

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
        return await _recorder.RecordAsync(Attach(path, null), ct).ConfigureAwait(false);
    }

    private void Start(TrackedProcessAppeared appeared, CancellationToken ct)
    {
        if (_running.TryGetValue(appeared.Game.Id, out Task? running) && !running.IsCompleted)
        {
            _log($"watch: pid {appeared.Pid} ({appeared.Game.Name}) appeared while a session for that game is running; one at a time");
            return;
        }

        _log(appeared.StalePath
            ? $"watch: pid {appeared.Pid} matched {appeared.Game.Name} BY FILE NAME ONLY — it runs from {appeared.ImagePath}, not the path on record; the session is keyed on the real path"
            : $"watch: pid {appeared.Pid} ({appeared.Game.Name}) appeared; starting a session");
        RecordRequest request = Attach(appeared.ImagePath, appeared.Game.Name);
        _running[appeared.Game.Id] = RunAndReportAsync(request, ct);
    }

    /// <summary>The session on its own task, reporting its own end; nothing else awaits a stored task.</summary>
    private async Task RunAndReportAsync(RecordRequest request, CancellationToken ct)
    {
        try
        {
            RecordedSession r = await Task.Run(() => _recorder.RecordAsync(request, ct), ct).ConfigureAwait(false);
            _log($"session {r.SessionGuid:N}: {r.Outcome.Reason}; {r.Finalize.Status} (tier {(int)r.Row.Tier}, exit={r.ExitStatus}, frames={r.Row.FrameCount})");
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

    private RecordRequest Attach(string normalisedExePath, string? gameName)
    {
        ExecutableFingerprint? observed = _identity.Read(normalisedExePath);
        return new RecordRequest
        {
            NormalisedExePath = normalisedExePath,
            Observed = observed,
            PayloadPath = _options.PayloadPath,
            Mode = CaptureMode.Attach,
            GameName = gameName,
        };
    }

    private void Reap()
    {
        foreach ((long gameId, Task task) in _running.ToArray())
        {
            if (task.IsCompleted)
            {
                _running.Remove(gameId);
            }
        }
    }

    private Task DrainRunningAsync()
    {
        Task[] all = [.. _running.Values];
        _running.Clear();
        return Task.WhenAll(all);
    }
}

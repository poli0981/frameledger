namespace FrameLedger.Application.Capture;

/// <summary>
/// D33 (owner decision 2026-09-26): the Agent's channel to the Overlay it is about to inject —
/// <c>Local\FrameLedger.Tolerate.&lt;pid&gt;</c> (<c>fl_tolerance.h</c>, <c>07_IPC</c> §D) — naming the family the game's
/// user-mode exception covers, so a module of that family loading AFTER the injection does not stop the Overlay.
/// </summary>
/// <remarks>
/// <para>
/// <b>Published before the guard is asked, held for the session.</b> The Overlay reads it once, on its init thread, before
/// its loader detour exists; a channel published after the injection would be read by nobody.
/// </para>
/// <para>
/// <b>A channel that could not be published is no exception.</b> <see cref="TryPublish"/> answers null — a name another
/// process already holds, a failed call — and the session then asks the guard to tolerate nothing, so the game is refused
/// as blocked. Injecting under a guard that tolerates the family while the Overlay does not would end in the Overlay's
/// own stop, which reads as the exception failing.
/// </para>
/// <para>
/// It carries a NAME and nothing else. The Overlay resolves it against its own compiled floor and honours only a
/// user-mode family; the 30 s re-scan keeps judging every signal.
/// </para>
/// </remarks>
public interface IOverlayToleranceChannel
{
    /// <summary>Publish <paramref name="family"/> for <paramref name="pid"/>; dispose the result when the session ends. Null when it could not be published.</summary>
    IDisposable? TryPublish(int pid, string family);
}

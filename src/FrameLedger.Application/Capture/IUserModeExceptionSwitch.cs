namespace FrameLedger.Application.Capture;

/// <summary>
/// D33 (owner decision 2026-09-26): Settings ▸ Capture's option that lets a game's user-mode anti-cheat exception apply at
/// all — <c>hooking.usermode_ac_exceptions</c>, off by default — as the capture path and the Agent's commands read it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Off suspends every exception and deletes none.</b> A grant stays on its row, and the next session with the option on
/// uses it again; with it off, a blocked game is the blocked game of before D33 — no tolerance named to the guard, no
/// channel to the Overlay, a Tier-2 session (<c>19_SAFETY</c> §The user-mode exception).
/// </para>
/// <para>
/// Read fresh on every ask, like <see cref="IKillSwitch"/>: the App flips one row in <c>settings</c>, and the next session
/// start and the next command take it.
/// </para>
/// </remarks>
public interface IUserModeExceptionSwitch
{
    /// <summary>True while the option is on and a game's exception may apply.</summary>
    ValueTask<bool> IsOnAsync(CancellationToken ct = default);
}

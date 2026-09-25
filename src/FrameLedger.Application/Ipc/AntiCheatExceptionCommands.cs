using FrameLedger.Application.Capture;
using FrameLedger.Application.Persistence;

namespace FrameLedger.Application.Ipc;

/// <summary>
/// D33 (owner decision 2026-09-26): what <see cref="AgentCommandHandler"/> needs to answer for a game's user-mode exception —
/// the option, the evidence, and the disclosure version this Agent grants against. One parameter rather than three, so a
/// composition either has the exception or does not.
/// </summary>
/// <param name="Switch"><c>hooking.usermode_ac_exceptions</c>, read on every command.</param>
/// <param name="Sessions">Where the successful Tier-1 sessions are counted.</param>
/// <param name="DisclosureVersion"><c>Shared.Safety.AntiCheatExceptionDisclosure.Version</c> under <c>--serve</c>.</param>
public sealed record AntiCheatExceptionCommands(IUserModeExceptionSwitch Switch, ISessionRepository Sessions, string DisclosureVersion);

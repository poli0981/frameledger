// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Domain.AntiCheat;

namespace FrameLedger.Application.Capture;

/// <summary>
/// Owner decision D48(b), beta.14. The guard's 30 s re-scan reads every process in the game's tree; one that is
/// starting (an engine's crash reporter, as its game dies) or gone could not be read, the scan refused, and the session
/// ended as a <see cref="SessionEndReason.SafetyUnhook"/> — a "safety" stop for a game that had simply exited, which also
/// ended a user-mode exception's trial for good. When the refusal says only that the guard COULD NOT READ and the game
/// itself has exited by the time the session concludes, the session ends as the game's exit
/// (<see cref="SessionEndReason.TargetExited"/>), its exit code deciding normal or crashed, and the scan's reason is kept
/// in the notes (<c>scan_at_exit=</c>). A finding — any anti-cheat the guard identified — is never relabelled, and neither
/// is a refusal while the game still runs.
/// </summary>
public static class ExitScanRelabel
{
    /// <summary>The guard's "could not read" refusals of the module check (<c>fl_guard.cpp</c> check 1) — never a finding.</summary>
    public static bool Applies(AntiCheatRefusalReason reason) =>
        reason is AntiCheatRefusalReason.ProcessUnreadable or AntiCheatRefusalReason.ModuleScanFailed or AntiCheatRefusalReason.ProcessTreeUnavailable;

    /// <summary>The end a refused re-scan concludes as: the game's exit when <see cref="Applies"/> and it has exited, else a safety unhook.</summary>
    public static SessionEndReason EndOf(AntiCheatVerdict? lastVerdict, bool targetExited) =>
        targetExited && lastVerdict is { IsAllowed: false } refused && Applies(refused.Reason)
            ? SessionEndReason.TargetExited
            : SessionEndReason.SafetyUnhook;
}

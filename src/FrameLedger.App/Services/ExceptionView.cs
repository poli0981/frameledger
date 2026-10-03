// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FrameLedger.Application.AntiCheat;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.AntiCheat;

namespace FrameLedger.App.Services;

/// <summary>
/// D33 (owner decision 2026-09-26): a blocked game's user-mode exception as the App shows it — in force (and, since D38, how
/// far its trial has come), eligible, being checked, or why not — read from the Agent's columns
/// (<see cref="GameRow.AcException"/>). The App decides nothing here: every state is a reading of what the Agent wrote, and
/// the grant itself is the Agent's to make.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed record ExceptionView(ExceptionViewKind Kind, string Text, string? Family, string? Signal, int Sessions, string? LapseText)
{
    /// <summary>An exception may be asked for now.</summary>
    public bool CanGrant => Kind == ExceptionViewKind.Eligible;

    /// <summary>An exception is on the row and may be withdrawn.</summary>
    public bool CanWithdraw => Kind == ExceptionViewKind.Granted;

    /// <summary>
    /// The row's exception, or null for a row it says nothing about (not blocked). Granted first — a grant stays until the
    /// Agent or the user ends it — then the block's kind, then a failed trial (D38: for good, whatever a check says), then
    /// the sweep's answer about THIS block.
    /// </summary>
    public static ExceptionView? Of(GameRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!row.BlockedByGuard)
        {
            return null;
        }

        AntiCheatExceptionState x = row.AcException;
        StoredBlock? block = StoredBlock.Parse(row.HookBlockedReason);
        string? lapse = x.LapsedReason is { } reason && x.LapsedAt is { } at
            ? string.Format(CultureInfo.CurrentCulture, Strings.Exception_Lapsed_Format, Formats.Date(at), LapseWords(reason))
            : null;
        if (x.IsGranted)
        {
            return new(ExceptionViewKind.Granted, GrantedText(x), x.Family, block?.Signal, x.Sessions, lapse);
        }

        if (block is not { IsExceptionable: true } b)
        {
            return new(ExceptionViewKind.NotEligible, Strings.Exception_State_NotAsked, block?.Family, block?.Signal, x.Sessions, lapse);
        }

        if (x.TrialFailedAt is { } failed)
        {
            return new(ExceptionViewKind.NotEligible, string.Format(CultureInfo.CurrentCulture, Strings.Exception_State_TrialFailed_Format, Formats.Date(failed)),
                b.Family, b.Signal, x.Sessions, lapse);
        }

        if (!string.Equals(x.CheckedBlock, row.HookBlockedReason, StringComparison.Ordinal))
        {
            return new(ExceptionViewKind.Checking, Strings.Exception_State_Checking, b.Family, b.Signal, x.Sessions, lapse);
        }

        if (x.Eligible)
        {
            return new(ExceptionViewKind.Eligible, Strings.Exception_State_Eligible, b.Family, b.Signal, x.Sessions, lapse);
        }

        // A guard that let the family through over a row that does not say eligible is an answer from before D38 (it
        // counted sessions); the Agent's next pass rewrites it, so until then it is being checked.
        return StoredBlock.Parse(x.Verdict) is { Reason: AntiCheatRefusalReason.AllowedUnderUserModeException }
            ? new(ExceptionViewKind.Checking, Strings.Exception_State_Checking, b.Family, b.Signal, x.Sessions, lapse)
            : new(ExceptionViewKind.NotEligible, WhyNot(b, x), b.Family, b.Signal, x.Sessions, lapse);
    }

    /// <summary>Why the last grant ended, in words.</summary>
    public static string LapseWords(string reason) => reason switch
    {
        UserModeExceptionLapse.Withdrawn => Strings.Exception_Lapse_Withdrawn,
        UserModeExceptionLapse.ExecutableChanged => Strings.Exception_Lapse_ExecutableChanged,
        UserModeExceptionLapse.NoLongerEligible => Strings.Exception_Lapse_NoLongerEligible,
        UserModeExceptionLapse.NewFinding => Strings.Exception_Lapse_NewFinding,
        UserModeExceptionLapse.SafetyUnhook => Strings.Exception_Lapse_SafetyUnhook,
        UserModeExceptionLapse.OverlayStopped => Strings.Exception_Lapse_OverlayStopped,
        UserModeExceptionLapse.SessionCrashed => Strings.Exception_Lapse_SessionCrashed,
        UserModeExceptionLapse.TrialFailed => Strings.Exception_Lapse_TrialFailed,
        _ => reason,
    };

    /// <summary>D38: a grant in its trial says how far it has come; one past it says only since when.</summary>
    private static string GrantedText(AntiCheatExceptionState x) =>
        UserModeExceptionRules.InTrial(x.Sessions)
            ? string.Format(CultureInfo.CurrentCulture, Strings.Exception_State_GrantedTrial_Format, Formats.Date(x.GrantedAt!.Value), x.Sessions,
                UserModeExceptionRules.TrialSessions)
            : string.Format(CultureInfo.CurrentCulture, Strings.Exception_State_Granted_Format, Formats.Date(x.GrantedAt!.Value));

    private static string WhyNot(StoredBlock block, AntiCheatExceptionState x)
    {
        StoredBlock? verdict = StoredBlock.Parse(x.Verdict);
        if (verdict is null)
        {
            return Strings.Exception_State_NotAsked;
        }

        // A plain pass: the tolerant scan found nothing of the family, so it confirmed nothing — the check DID finish
        // (it used to be said as "could not finish").
        if (verdict.Value.Reason == AntiCheatRefusalReason.Allow)
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.Exception_State_NotFound_Format, block.Family);
        }

        if (!verdict.Value.IsExceptionable && verdict.Value.Family.Length == 0)
        {
            return Strings.Exception_State_CouldNotVerify;
        }

        // The same finding back means the guard does not honour this family: it is not user-mode throughout.
        return string.Equals(verdict.Value.Family, block.Family, StringComparison.Ordinal)
            ? string.Format(CultureInfo.CurrentCulture, Strings.Exception_State_NotUserMode_Format, block.Family)
            : string.Format(CultureInfo.CurrentCulture, Strings.Exception_State_Refused_Format, verdict.Value.Family, verdict.Value.Signal);
    }
}

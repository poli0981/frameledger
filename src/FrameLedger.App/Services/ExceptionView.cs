using System.Globalization;
using FrameLedger.Application.AntiCheat;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.AntiCheat;

namespace FrameLedger.App.Services;

/// <summary>
/// D33 (owner decision 2026-09-26): a blocked game's user-mode exception as the App shows it — in force, eligible, being
/// checked, or why not — read from the Agent's columns (<see cref="GameRow.AcException"/>). The App decides nothing here:
/// every state is a reading of what the Agent wrote, and the grant itself is the Agent's to make.
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
    /// Agent or the user ends it — then the block's kind, then the sweep's answer about THIS block.
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
            return new(ExceptionViewKind.Granted, string.Format(CultureInfo.CurrentCulture, Strings.Exception_State_Granted_Format, Formats.Date(x.GrantedAt!.Value)),
                x.Family, block?.Signal, x.Sessions, lapse);
        }

        if (block is not { IsExceptionable: true } b)
        {
            return new(ExceptionViewKind.NotEligible, Strings.Exception_State_NotAsked, block?.Family, block?.Signal, x.Sessions, lapse);
        }

        if (!string.Equals(x.CheckedBlock, row.HookBlockedReason, StringComparison.Ordinal))
        {
            return new(ExceptionViewKind.Checking, Strings.Exception_State_Checking, b.Family, b.Signal, x.Sessions, lapse);
        }

        return x.Eligible
            ? new(ExceptionViewKind.Eligible, Strings.Exception_State_Eligible, b.Family, b.Signal, x.Sessions, lapse)
            : new(NotEligibleKind(x), WhyNot(b, x), b.Family, b.Signal, x.Sessions, lapse);
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
        _ => reason,
    };

    /// <summary>The guard let the family through and only the evidence is short — the one "not yet" the Settings list shows.</summary>
    private static ExceptionViewKind NotEligibleKind(AntiCheatExceptionState x) =>
        StoredBlock.Parse(x.Verdict) is { Reason: AntiCheatRefusalReason.AllowedUnderUserModeException }
            ? ExceptionViewKind.TooFewSessions
            : ExceptionViewKind.NotEligible;

    private static string WhyNot(StoredBlock block, AntiCheatExceptionState x)
    {
        StoredBlock? verdict = StoredBlock.Parse(x.Verdict);
        if (verdict is null)
        {
            return Strings.Exception_State_NotAsked;
        }

        if (verdict.Value.Reason == AntiCheatRefusalReason.AllowedUnderUserModeException)
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.Exception_State_TooFew_Format, x.Sessions);
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

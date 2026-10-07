// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Services;

/// <summary>D33: how an ask for a user-mode exception ended, and — for a refusal — what the Agent named.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed record AntiCheatExceptionResult(AntiCheatExceptionOutcome Outcome, string? Detail = null, RefusedAck? Refusal = null)
{
    public static AntiCheatExceptionResult Of(AntiCheatExceptionOutcome outcome) => new(outcome);

    /// <summary>The outcome a user wanted: the exception made, or withdrawn.</summary>
    public bool Succeeded => Outcome is AntiCheatExceptionOutcome.Granted or AntiCheatExceptionOutcome.Withdrawn;

    /// <summary>What to tell the user about <paramref name="gameName"/>, or null when there is nothing to say (a declined disclosure).</summary>
    public string? MessageFor(string gameName) => Outcome switch
    {
        AntiCheatExceptionOutcome.Granted => string.Format(CultureInfo.CurrentCulture, Strings.Exception_Result_Granted_Format, gameName),
        AntiCheatExceptionOutcome.Withdrawn => string.Format(CultureInfo.CurrentCulture, Strings.Exception_Result_Withdrawn_Format, gameName),
        AntiCheatExceptionOutcome.Refused => string.Format(CultureInfo.CurrentCulture, Strings.Exception_Result_Refused_Format, gameName,
            RefusalText() ?? Detail ?? Strings.Common_NotAvailable),
        AntiCheatExceptionOutcome.VersionMismatch => Shared.Strings.Safety_Exception_VersionMismatch,
        AntiCheatExceptionOutcome.OptionOff => Shared.Strings.Safety_Exception_OptionOff,
        // A grant and a withdrawal alike (beta.18): "Nothing was enabled." was the hooking consent's words, untrue for a withdrawal.
        AntiCheatExceptionOutcome.AgentUnavailable => Strings.Agent_NotConnected_NothingChanged,
        AntiCheatExceptionOutcome.Failed => Detail ?? Strings.Common_NotAvailable,
        _ => null,
    };

    /// <summary>Why no exception was made, in the user's language: a failed trial (D38), the finding, or the block's kind.</summary>
    public string? RefusalText()
    {
        if (Refusal is null)
        {
            return null;
        }

        return Refusal.Reason switch
        {
            "TrialFailed" => string.Format(CultureInfo.CurrentCulture, Strings.Exception_State_TrialFailed_Format, TrialFailedDate(Refusal.Signal)),
            "BlockNotExceptionable" => Strings.Exception_State_NotAsked,
            "PreScanFailed" or "RulesUnreadable" or "RulesMalformed" or "RulesIncomplete" => Strings.Exception_State_CouldNotVerify,
            _ => string.Format(CultureInfo.CurrentCulture, Strings.Exception_State_Refused_Format,
                Refusal.Family ?? Formats.GuardReasonText(Refusal.Reason), Refusal.Signal ?? Strings.Common_NotAvailable),
        };
    }

    /// <summary>D38: the Agent sends when the trial failed as unix milliseconds; anything else reads as not available.</summary>
    private static string TrialFailedDate(string? unixMs) =>
        long.TryParse(unixMs, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ms)
            ? Formats.Date(DateTimeOffset.FromUnixTimeMilliseconds(ms))
            : Strings.Common_NotAvailable;
}

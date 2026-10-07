// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Services;

/// <summary>
/// <c>08_UI</c> §Notifications policy — "safety events are never toasts": <c>CaptureRefused</c>, <c>SafetyUnhook</c>
/// and <c>CaptureDegraded</c> become persistent notices the shell shows as <c>InfoBar</c>s above every page, with
/// the specific signal named in plain language (<c>Safety_Refused_*</c>, <c>Safety_Unhooked_Format</c>) and the
/// one action FR-14 allows — acknowledging that the session is recorded without measuring — which is what the
/// Agent already does; the notice tells the user why their data changed fidelity. <c>CaptureError</c> is a
/// non-fatal warning and goes to the strip.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed class SafetyNotices : IDisposable
{
    private readonly IAgentLink _agent;
    private readonly IMessageStrip _strip;
    private readonly TimeProvider _clock;
    private readonly UiThread _ui = new();

    public SafetyNotices(IAgentLink agent, IMessageStrip strip, TimeProvider? clock = null)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _strip = strip ?? throw new ArgumentNullException(nameof(strip));
        _clock = clock ?? TimeProvider.System;
        _agent.EventReceived += OnEvent;
    }

    public ObservableCollection<SafetyNotice> Items { get; } = [];

    public void Dismiss(SafetyNotice notice) => Items.Remove(notice);

    public void Dispose() => _agent.EventReceived -= OnEvent;

    /// <summary>The notice an envelope produces, or null when it is not a safety event (a test's window).</summary>
    public SafetyNotice? Translate(IpcEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        DateTimeOffset now = _clock.GetUtcNow();
        switch (envelope.Type)
        {
            case IpcMessageType.CaptureRefused when IpcCodec.Payload<CaptureRefusedEvent>(envelope) is { } refused:
                return new SafetyNotice(SafetyNoticeKind.Refused,
                    string.Format(CultureInfo.CurrentCulture, Strings.Notice_Refused_Title_Format, refused.GameName ?? Strings.Common_NotAvailable),
                    // "The session is still recorded" is true for every refusal since 2026-09-22: the loop holds a refused session
                    // open, unhooked, until the game exits (it was untrue for a process that could not be opened, 2026-09-21).
                    TurnedOff(RefusalText(refused.Reason, refused.GuardReason, refused.Family, refused.Signal) + " " + Strings.Notice_Refused_Recording, refused.HookingTurnedOff), now);
            case IpcMessageType.SafetyUnhook when IpcCodec.Payload<SafetyUnhookEvent>(envelope) is { } unhooked:
                return new SafetyNotice(SafetyNoticeKind.Unhooked, Strings.Notice_Unhooked_Title, TurnedOff(UnhookText(unhooked), unhooked.HookingTurnedOff), now);
            // The Agent's pre-scan of the library turned hooking off for a game the user had turned it on for (2026-09-25):
            // not a session event, so none of a refusal's "this run is still recorded" — no run is going on.
            // Its reason is the guard's own, and it is sent only for a finding about the game, which names its family.
            case IpcMessageType.HookingTurnedOff when IpcCodec.Payload<HookingTurnedOffEvent>(envelope) is { } off:
                return new SafetyNotice(SafetyNoticeKind.HookingOff,
                    string.Format(CultureInfo.CurrentCulture, Strings.Notice_HookingOff_Title_Format, off.GameName ?? Strings.Common_NotAvailable),
                    TurnedOff(GuardText(off.Reason, off.Family, off.Signal) ?? Shared.Strings.Safety_Refused_Unnamed, turnedOff: true), now);
            // The session's end in words (beta.18): it printed the enum's name — "The hook stopped (WriterSelfDisabled)" —
            // and said the session continued without measuring, while a stop ends it (07_IPC: measurement STOPPED).
            case IpcMessageType.CaptureDegraded when IpcCodec.Payload<CaptureDegradedEvent>(envelope) is { } degraded:
                return new SafetyNotice(SafetyNoticeKind.Degraded, Strings.Notice_Degraded_Title,
                    Formats.EndReasonText(degraded.Reason) ?? string.Format(CultureInfo.CurrentCulture, Strings.Notice_Degraded_Unknown_Format, degraded.Reason), now);
            default:
                return null;
        }
    }

    /// <summary>The finding turned the game's hooking off (2026-09-22): the notice says so, because the page will.</summary>
    private static string TurnedOff(string text, bool turnedOff) => turnedOff ? text + " " + Shared.Strings.Safety_HookingTurnedOff : text;

    /// <summary>
    /// A driver or service that started on the PC mid-session is said as that (beta.13, §S23-3) — not "detected while the
    /// game was running … until you enable it again", which was untrue twice: such a finding turns no hooking off, and a
    /// finding about the game turns it off for good (the turned-off sentence follows). A re-scan that could not look while
    /// the game still ran is said as that too (beta.18): it named its signal as a finding, "Access is denied was detected".
    /// </summary>
    private static string UnhookText(SafetyUnhookEvent unhooked) => unhooked switch
    {
        { Family: { } family } when Formats.IsMachineWide(unhooked.GuardReason) =>
            string.Format(CultureInfo.CurrentCulture, Shared.Strings.Safety_Unhooked_MachineWide_Format, family, unhooked.Signal ?? Strings.Common_NotAvailable),
        { Family: null, GuardReason: { Length: > 0 } guardReason } =>
            string.Format(CultureInfo.CurrentCulture, Shared.Strings.Safety_Unhooked_Unnamed_Format, Formats.GuardReasonText(guardReason)),
        _ => string.Format(CultureInfo.CurrentCulture, Shared.Strings.Safety_Unhooked_Format, unhooked.Family ?? unhooked.Signal ?? Strings.Common_NotAvailable),
    };

    /// <summary>
    /// A refusal in words. <paramref name="reason"/> is the session's end; <paramref name="guardReason"/> the guard's own,
    /// sent for every refusal it gave since beta.18 (beside a family only, before).
    /// </summary>
    private static string RefusalText(string reason, string? guardReason, string? family, string? signal) => reason switch
    {
        "PreScanCouldNotVerify" => Shared.Strings.Safety_Refused_CouldNotVerify,
        "TargetUnreadable" => Shared.Strings.Safety_Refused_TargetUnreadable,
        // What the guard found, or could not do; from an agent that sent no reason beside no family, the end's own words.
        "RefusedByGuard" => GuardText(guardReason, family, signal) ?? Strings.End_RefusedByGuard,
        // The gate's own refusals (beta.18) — Disable all hooking, a game blocked before — are the user's switch or the row's,
        // never a finding. The gate labels them in the family's place, and the kill switch's notice read "kill switch was
        // detected in this game (the global 'disable all hooking' switch is on (FR-2.4); …)".
        // A name this build has no words for (a newer agent's) is named as it is, never as something detected.
        _ => Formats.EndReasonText(reason) ?? GuardText(guardReason, family, signal)
            ?? (string.IsNullOrEmpty(reason) ? Shared.Strings.Safety_Refused_Unnamed : Formats.GuardSentence(reason)),
    };

    /// <summary>
    /// What the guard said, as a sentence: an anti-cheat it named, found in this game or on this PC; a refusal no family names
    /// (beta.8) in its own words — never its signal as the family, which read "Access is denied was detected in this game".
    /// Null when it said nothing.
    /// </summary>
    private static string? GuardText(string? guardReason, string? family, string? signal) =>
        family is { Length: > 0 }
            ? Formats.NamedRefusal(guardReason, family, signal ?? Strings.Common_NotAvailable)
            : guardReason is { Length: > 0 } ? Formats.GuardSentence(guardReason) : null;

    private void OnEvent(object? sender, AgentEventArgs e)
    {
        IpcEnvelope envelope = e.Envelope;
        if (string.Equals(envelope.Type, IpcMessageType.CaptureError, StringComparison.Ordinal) && IpcCodec.Payload<CaptureErrorEvent>(envelope) is { } error)
        {
            _ui.Post(() => _strip.Warn(Strings.Notice_Error_Title, error.Code + ": " + error.Message));
            return;
        }

        SafetyNotice? notice = Translate(envelope);
        if (notice is not null)
        {
            _ui.Post(() => Items.Insert(0, notice));
        }
    }
}

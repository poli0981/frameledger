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
                    RefusalText(refused.Reason, refused.Family, refused.Signal) + " " + Strings.Notice_Refused_Recording, now);
            case IpcMessageType.SafetyUnhook when IpcCodec.Payload<SafetyUnhookEvent>(envelope) is { } unhooked:
                return new SafetyNotice(SafetyNoticeKind.Unhooked, Strings.Notice_Unhooked_Title,
                    string.Format(CultureInfo.CurrentCulture, Shared.Strings.Safety_Unhooked_Format, unhooked.Family ?? unhooked.Signal ?? Strings.Common_NotAvailable), now);
            case IpcMessageType.CaptureDegraded when IpcCodec.Payload<CaptureDegradedEvent>(envelope) is { } degraded:
                return new SafetyNotice(SafetyNoticeKind.Degraded, Strings.Notice_Degraded_Title,
                    string.Format(CultureInfo.CurrentCulture, Strings.Notice_Degraded_Body_Format, degraded.Reason), now);
            default:
                return null;
        }
    }

    private static string RefusalText(string reason, string? family, string? signal)
    {
        if (string.Equals(reason, "PreScanCouldNotVerify", StringComparison.Ordinal))
        {
            return Shared.Strings.Safety_Refused_CouldNotVerify;
        }

        string? named = family ?? signal;
        return named is null
            ? Shared.Strings.Safety_Refused_Unnamed
            : string.Format(CultureInfo.CurrentCulture, Shared.Strings.Safety_Refused_Named_Format, named, signal ?? reason);
    }

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

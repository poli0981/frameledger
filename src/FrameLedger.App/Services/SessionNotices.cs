// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Services;

/// <summary>
/// FR-3.8's post-session notice inside the App: "saved" or "discarded" in the snackbar while the window is on screen — the
/// tray's balloon carries it otherwise (P3 PR-8b, one event → one channel), and a safety stop is never a toast (08_UI
/// §Notifications policy). One subscription for the App's life (beta.23).
/// </summary>
/// <remarks>
/// It lived in the Dashboard's view model, and it reached every page only because no Dashboard ever went away: each visit
/// made a new one, the root container kept every one of them (a transient <see cref="IDisposable"/>), and each stayed
/// subscribed — twenty visits were twenty notices for one session, of which WPF UI's snackbar showed the last, and twenty
/// reloads of the Dashboard's lists on the UI thread (<c>spike-notes</c> §21). The Dashboard now listens only while it is on
/// screen, so the notice has its own home.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed class SessionNotices : IDisposable
{
    private readonly IAgentLink _agent;
    private readonly IMessageStrip _strip;
    private readonly IShellPresence? _presence;
    private readonly UiThread _ui = new();
    private readonly Lock _lock = new();

    // The names the start events gave, for a completion that carries none (an Agent before 2026-09-23 sent none).
    private readonly Dictionary<Guid, string> _names = [];

    public SessionNotices(IAgentLink agent, IMessageStrip strip, IShellPresence? presence = null)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _strip = strip ?? throw new ArgumentNullException(nameof(strip));
        _presence = presence;
        _agent.EventReceived += OnAgentEvent;
    }

    public void Dispose() => _agent.EventReceived -= OnAgentEvent;

    private void OnAgentEvent(object? sender, AgentEventArgs e)
    {
        IpcEnvelope envelope = e.Envelope;
        if (string.Equals(envelope.Type, IpcMessageType.SessionStarted, StringComparison.Ordinal)
            && IpcCodec.Payload<SessionStartedEvent>(envelope) is { GameName: { Length: > 0 } started } start)
        {
            lock (_lock)
            {
                _names[start.SessionGuid] = started;
            }
        }
        else if (string.Equals(envelope.Type, IpcMessageType.SessionCompleted, StringComparison.Ordinal)
                 && IpcCodec.Payload<SessionCompletedEvent>(envelope) is { } completed)
        {
            string? known;
            lock (_lock)
            {
                _ = _names.Remove(completed.SessionGuid, out known);
            }

            // The session's own name (2026-09-23): the Dashboard's card may show another session, and a Tier-2 one had none.
            string? name = completed.GameName is { Length: > 0 } given ? given : known;
            _ui.Post(() => Notice(completed, name));
        }
    }

    private void Notice(SessionCompletedEvent completed, string? name)
    {
        // A faulted session is the strip's CaptureError already; "discarded" would say something that did not happen.
        if (_presence is { IsShown: false } || string.Equals(completed.Finalize, "faulted", StringComparison.Ordinal))
        {
            return;
        }

        string game = name is { Length: > 0 } ? name : Strings.Common_NotAvailable;
        if (completed.SessionId is not null)
        {
            _strip.Success(Strings.Dashboard_Live_Header, string.Format(CultureInfo.CurrentCulture, Strings.Dashboard_SessionSaved_Format, game));
        }
        else
        {
            _strip.Info(Strings.Dashboard_Live_Header, string.Format(CultureInfo.CurrentCulture, Strings.Dashboard_SessionDiscarded_Format, game));
        }
    }
}

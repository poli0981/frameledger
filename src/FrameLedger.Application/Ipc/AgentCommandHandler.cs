using FrameLedger.Application.AntiCheat;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Consent;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Vulkan;
using FrameLedger.Application.Watch;
using FrameLedger.Domain.AntiCheat;
using FrameLedger.Domain.Consent;
using FrameLedger.Shared.Ipc;
using FrameLedger.Shared.Safety;

namespace FrameLedger.Application.Ipc;

/// <summary>
/// The command half of <c>07_IPC</c> §Messages (P3 PR-1b): what a client may ASK for. <c>07_IPC</c> §The pipe is
/// not a trust boundary is the rule every method here follows — no inbound message asserts a safety fact; the
/// Agent establishes each one itself. <c>SetHookEnabled</c> is where that bites: the pre-scan runs here, a block
/// is recorded here, and the consent stamp — the Agent's clock, the Agent's provenance — is stamped here or not
/// at all. ~~Until FR-2.1's reviewed disclosure exists (P3 PR-4) it is not at all~~ Since PR-4 (2026-09-13) it is
/// stamped when the client's <c>disclosureVersion</c> equals this Agent's (D14) — <c>ConsentProvenance.ConsentDialog</c>,
/// this clock — and answered <c>Error DisclosureVersionMismatch</c> otherwise; a composition that wired no version
/// still answers <c>Error DisclosureUnavailable</c>, and in every case nothing is written but a block until then.
/// </summary>
public sealed class AgentCommandHandler
{
    private readonly IGameRepository _games;
    private readonly IGameConsentStore _consent;
    private readonly IGuardBypassStore? _bypass;
    private readonly IAntiCheatGuard _guard;
    private readonly IExecutableIdentitySource _identity;
    private readonly CaptureOrchestrator _orchestrator;
    private readonly CapturePause _pause;
    private readonly IAgentLifetime _lifetime;
    private readonly Func<CancellationToken, ValueTask<string>> _updateRules;
    private readonly string? _disclosureVersion;
    private readonly TimeProvider _clock;
    private readonly VkLayerReconciler? _layer;
    private readonly Func<CancellationToken, ValueTask<SweepRetentionAck>>? _sweepRetention;

    /// <summary>
    /// <c>disclosureVersion</c> is the version of FR-2.1's reviewed disclosure this Agent carries
    /// (<c>Shared.Safety.SafetyDisclosure.Version</c> under <c>--serve</c>); while it is null no <c>SetHookEnabled true</c>
    /// stamps anything. <c>clock</c> is the stamp's — the Agent's, never the client's.
    /// </summary>
    public AgentCommandHandler(IGameRepository games, IGameConsentStore consent, IAntiCheatGuard guard, IExecutableIdentitySource identity,
        CaptureOrchestrator orchestrator, CapturePause pause, IAgentLifetime lifetime, Func<CancellationToken, ValueTask<string>> updateRules,
        string? disclosureVersion = null, TimeProvider? clock = null, VkLayerReconciler? layer = null,
        Func<CancellationToken, ValueTask<SweepRetentionAck>>? sweepRetention = null, IGuardBypassStore? bypass = null)
    {
        // Owner decision 2026-09-21: the per-game guard bypass. Null where nothing composed it, and SetGuardBypass then
        // answers DisclosureUnavailable - a handler that cannot record the acknowledgement must not pretend it did.
        _bypass = bypass;
        // P4 PR-7: Tools ▸ Database maintenance's sweep — composed under --serve, absent (UnknownType) where nothing wired it.
        _sweepRetention = sweepRetention;
        _clock = clock ?? TimeProvider.System;
        // P4 PR-2: the layer's registration follows every consent change this handler makes (12_BUILD §The Vulkan
        // layer is not registered at install time); null in tests that do not care, and the sweep reconciles anyway.
        _layer = layer;
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _consent = consent ?? throw new ArgumentNullException(nameof(consent));
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _pause = pause ?? throw new ArgumentNullException(nameof(pause));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        _updateRules = updateRules ?? throw new ArgumentNullException(nameof(updateRules));
        _disclosureVersion = disclosureVersion;
    }

    /// <summary>FR-3.9's global pause, as <c>StatusAck.paused</c> reports it.</summary>
    public bool IsPaused => _pause.IsPaused;

    /// <summary>The ack, or null when <paramref name="request"/> is not a command this half answers.</summary>
    public async ValueTask<byte[]?> HandleAsync(IpcEnvelope request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Type switch
        {
            IpcMessageType.SetWatchlist => await SetWatchlistAsync(request, ct).ConfigureAwait(false),
            IpcMessageType.LaunchGame => await LaunchAsync(request, ct).ConfigureAwait(false),
            IpcMessageType.SetHookEnabled => await SetHookEnabledAsync(request, ct).ConfigureAwait(false),
            IpcMessageType.SetGuardBypass => await SetGuardBypassAsync(request, ct).ConfigureAwait(false),
            IpcMessageType.PauseCapture => IpcCodec.Encode(IpcMessageType.PauseAck, request.Id, new PauseAck(_pause.Set(true))),
            IpcMessageType.ResumeCapture => IpcCodec.Encode(IpcMessageType.PauseAck, request.Id, new PauseAck(_pause.Set(false))),
            IpcMessageType.StopSession => Stop(request),
            IpcMessageType.UpdateRules => IpcCodec.Encode(IpcMessageType.UpdateRulesAck, request.Id, new UpdateRulesAck(await _updateRules(ct).ConfigureAwait(false))),
            IpcMessageType.Shutdown => Shutdown(request),
            IpcMessageType.SweepRetention when _sweepRetention is not null =>
                IpcCodec.Encode(IpcMessageType.SweepRetentionAck, request.Id, await _sweepRetention(ct).ConfigureAwait(false)),
            _ => null,
        };
    }

    /// <summary>
    /// Identity only (<c>07_IPC</c>: "carries no hooking state"): every entry's row is ensured, hooking off when new.
    /// Nothing is removed — removing a game is the App's FR-1.4 (with its "keep the sessions?" question), never a
    /// bulk message's.
    /// </summary>
    private async ValueTask<byte[]> SetWatchlistAsync(IpcEnvelope request, CancellationToken ct)
    {
        SetWatchlistRequest? payload = IpcCodec.Payload<SetWatchlistRequest>(request);
        if (payload is null)
        {
            return Malformed(request, "SetWatchlist carries no payload");
        }

        var games = new List<WatchlistGame>();
        var unreadable = new List<string>();
        foreach (WatchlistEntry entry in payload.Entries)
        {
            string path = _identity.Normalise(entry.ExePath);
            ExecutableFingerprint? fingerprint = _identity.Read(path);
            if (fingerprint is null)
            {
                unreadable.Add(entry.ExePath);
                continue;
            }

            GameRow row = await _games.EnsureAsync(fingerprint.Value, Path.GetFileNameWithoutExtension(path), ct).ConfigureAwait(false);
            games.Add(new WatchlistGame(row.Id, row.Fingerprint.ExePath, row.Name));
        }

        return IpcCodec.Encode(IpcMessageType.WatchlistAck, request.Id, new WatchlistAck(games, unreadable));
    }

    private async ValueTask<byte[]> LaunchAsync(IpcEnvelope request, CancellationToken ct)
    {
        LaunchGameRequest? payload = IpcCodec.Payload<LaunchGameRequest>(request);
        if (payload is null)
        {
            return Malformed(request, "LaunchGame carries no payload");
        }

        LaunchResult result = await _orchestrator.LaunchAsync(payload.GameId, payload.Arguments ?? string.Empty, ct).ConfigureAwait(false);
        return IpcCodec.Encode(IpcMessageType.LaunchAck, request.Id,
            new LaunchAck(payload.GameId, result.Outcome == LaunchOutcome.Accepted, result.Outcome.ToString(), result.SessionGuid));
    }

    /// <summary>
    /// The Agent re-runs the static pre-scan and stamps — or refuses — itself (<c>07_IPC</c> §The pipe is not a
    /// trust boundary). A revoke needs no scan. An enable: the scan first, a block recorded and answered
    /// <c>Refused</c>; a clean scan is then stamped by <see cref="StampAsync"/> only when the client's disclosure
    /// version is this Agent's, so nothing is ever stamped on the strength of a client's word.
    /// </summary>
    private async ValueTask<byte[]> SetHookEnabledAsync(IpcEnvelope request, CancellationToken ct)
    {
        SetHookEnabledRequest? payload = IpcCodec.Payload<SetHookEnabledRequest>(request);
        if (payload is null)
        {
            return Malformed(request, "SetHookEnabled carries no payload");
        }

        GameRow? game = await FindGameAsync(payload.GameId, ct).ConfigureAwait(false);
        if (game is null)
        {
            return Error(request, IpcErrorCode.UnknownGame, $"no games row has id {payload.GameId}");
        }

        string path = game.Fingerprint.ExePath;
        if (!payload.Enabled)
        {
            ConsentWriteOutcome revoked = await _consent.RevokeAsync(path, ct).ConfigureAwait(false);
            await ReconcileLayerAsync(ct).ConfigureAwait(false);
            return IpcCodec.Encode(IpcMessageType.HookEnabledAck, request.Id, new HookEnabledAck(game.Id, Enabled: false, revoked.ToString(), Prescan: null));
        }

        ExecutableFingerprint? fingerprint = _identity.Read(path);
        if (fingerprint is null)
        {
            return Error(request, IpcErrorCode.ExecutableUnreadable, $"{path} could not be read, so nothing can be scanned or stamped");
        }

        string directory = Path.GetDirectoryName(path) ?? path;
        AntiCheatVerdict verdict = await _guard.PreScanGameDirectoryAsync(directory, ct).ConfigureAwait(false);
        if (!verdict.IsAllowed)
        {
            // A refusal, or a scan that reached no answer: the store records which (a default verdict is
            // "could not verify", never a block), and both are answered as a refusal to enable.
            await _consent.RecordGuardBlockAsync(fingerprint.Value, verdict, ct).ConfigureAwait(false);

            // THE USER'S BYPASS (owner decision 2026-09-21). The finding above is still WRITTEN - the block stays on
            // the row and stays true - and then, for a game whose owner accepted the bypass disclosure for this very
            // executable, the consent is stamped anyway. Only the guard's judgement is overruled: a verdict that is a
            // fact (none reaches this call today) still refuses.
            GameConsentRecord recorded = await _consent.FindAsync(path, ct).ConfigureAwait(false);
            bool judgement = verdict.Reason == AntiCheatRefusalReason.Allow || AntiCheatVerdict.IsGuardJudgement(verdict.Reason);
            if (recorded.GuardBypassAcknowledged && recorded.Fingerprint.Matches(fingerprint.Value) && judgement)
            {
                return await StampAsync(request, game, fingerprint.Value, payload.DisclosureVersion, "bypassed", ct).ConfigureAwait(false);
            }

            await ReconcileLayerAsync(ct).ConfigureAwait(false);
            string reason = verdict.Reason == AntiCheatRefusalReason.Allow ? "PreScanCouldNotVerify" : verdict.Reason.ToString();
            return IpcCodec.Encode(IpcMessageType.Refused, request.Id,
                new RefusedAck(game.Id, reason, NullIfEmpty(verdict.Family), NullIfEmpty(verdict.Signal)));
        }

        return await StampAsync(request, game, fingerprint.Value, payload.DisclosureVersion, "clean", ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Owner decision 2026-09-21: record, or withdraw, the user's acknowledgement of the guard-bypass disclosure for one
    /// game. It writes two columns and enables nothing - hooking, consent and the block are untouched - and it is
    /// refused for any disclosure version but this Agent's own, for an executable that cannot be read, and where no
    /// store was composed. The pipe is not a trust boundary, so everything the App checked is checked again here.
    /// </summary>
    private async ValueTask<byte[]> SetGuardBypassAsync(IpcEnvelope request, CancellationToken ct)
    {
        SetGuardBypassRequest? payload = IpcCodec.Payload<SetGuardBypassRequest>(request);
        if (payload is null)
        {
            return Malformed(request, "SetGuardBypass carries no payload");
        }

        GameRow? game = await FindGameAsync(payload.GameId, ct).ConfigureAwait(false);
        if (game is null)
        {
            return Error(request, IpcErrorCode.UnknownGame, $"no games row has id {payload.GameId}");
        }

        if (_bypass is null)
        {
            return Error(request, IpcErrorCode.DisclosureUnavailable, "this Agent was composed without the guard-bypass store; nothing was recorded");
        }

        string path = game.Fingerprint.ExePath;
        if (!payload.Enabled)
        {
            ConsentWriteOutcome revoked = await _bypass.RevokeAsync(path, ct).ConfigureAwait(false);
            return IpcCodec.Encode(IpcMessageType.GuardBypassAck, request.Id, new GuardBypassAck(game.Id, Enabled: false, revoked.ToString()));
        }

        if (!string.Equals(payload.DisclosureVersion, GuardBypassDisclosure.Version, StringComparison.Ordinal))
        {
            return Error(request, IpcErrorCode.DisclosureVersionMismatch,
                $"the client showed bypass disclosure '{payload.DisclosureVersion ?? "(none)"}' and this Agent records '{GuardBypassDisclosure.Version}': nothing was recorded - restart both");
        }

        ExecutableFingerprint? fingerprint = _identity.Read(path);
        if (fingerprint is null)
        {
            return Error(request, IpcErrorCode.ExecutableUnreadable, $"{path} could not be read, so there is no executable to record the bypass for");
        }

        ConsentWriteOutcome outcome = await _bypass.RecordAsync(new GuardBypassAcknowledgement
        {
            Fingerprint = fingerprint.Value,
            DisclosureVersion = GuardBypassDisclosure.Version,
            AcknowledgedAt = _clock.GetUtcNow(),
        }, ct).ConfigureAwait(false);
        return IpcCodec.Encode(IpcMessageType.GuardBypassAck, request.Id,
            new GuardBypassAck(game.Id, Enabled: outcome == ConsentWriteOutcome.Written, outcome.ToString()));
    }

    /// <summary>
    /// The stamp (P3 PR-4, D14): the client's version must be this Agent's — a client that showed other text, or
    /// none, gets <c>DisclosureVersionMismatch</c> and nothing written; a composition with no version at all gets
    /// <c>DisclosureUnavailable</c>. The record carries this Agent's version, this Agent's clock, and
    /// <see cref="ConsentProvenance.ConsentDialog"/>; the store's own rules (a block is never cleared by a grant,
    /// a stale fingerprint under a block is refused) still apply and come back as the ack's <c>outcome</c>.
    /// </summary>
    private async ValueTask<byte[]> StampAsync(IpcEnvelope request, GameRow game, ExecutableFingerprint fingerprint, string? clientVersion, string prescan, CancellationToken ct)
    {
        if (_disclosureVersion is null)
        {
            return Error(request, IpcErrorCode.DisclosureUnavailable,
                "the pre-scan is clean, and nothing was stamped: this Agent was composed without FR-2.1's disclosure version; "
                + "hooking is enabled through `FrameLedger.Agent --console consent grant` on such a build");
        }

        if (!string.Equals(clientVersion, _disclosureVersion, StringComparison.Ordinal))
        {
            return Error(request, IpcErrorCode.DisclosureVersionMismatch,
                $"the client showed disclosure '{clientVersion ?? "(none)"}' and this Agent stamps against '{_disclosureVersion}': nothing was stamped — restart both");
        }

        ConsentWriteOutcome outcome = await _consent.RecordOperatorAcknowledgementAsync(new OperatorAcknowledgement
        {
            Fingerprint = fingerprint,
            DisclosureVersion = _disclosureVersion,
            AcknowledgedAt = _clock.GetUtcNow(),
            Provenance = ConsentProvenance.ConsentDialog,
        }, ct).ConfigureAwait(false);
        if (outcome == ConsentWriteOutcome.Written)
        {
            await ReconcileLayerAsync(ct).ConfigureAwait(false);
        }

        return IpcCodec.Encode(IpcMessageType.HookEnabledAck, request.Id,
            new HookEnabledAck(game.Id, Enabled: outcome == ConsentWriteOutcome.Written, outcome.ToString(), Prescan: prescan));
    }

    private async ValueTask ReconcileLayerAsync(CancellationToken ct)
    {
        if (_layer is not null)
        {
            _ = await _layer.ReconcileAsync(ct).ConfigureAwait(false);
        }
    }

    private byte[] Stop(IpcEnvelope request)
    {
        StopSessionRequest? payload = IpcCodec.Payload<StopSessionRequest>(request);
        if (payload is null)
        {
            return Malformed(request, "StopSession carries no payload");
        }

        bool accepted = _orchestrator.StopSession(payload.SessionGuid);
        return IpcCodec.Encode(IpcMessageType.StopAck, request.Id,
            new StopAck(payload.SessionGuid, accepted, accepted ? null : "no running session started by the watcher or a launch carries that guid"));
    }

    private byte[] Shutdown(IpcEnvelope request)
    {
        byte[] ack = IpcCodec.Encode(IpcMessageType.ShutdownAck, request.Id, new ShutdownAck());
        _lifetime.RequestShutdown();
        return ack;
    }

    private async ValueTask<GameRow?> FindGameAsync(long gameId, CancellationToken ct)
    {
        IReadOnlyList<GameRow> rows = await _games.ListAsync(ct).ConfigureAwait(false);
        return rows.FirstOrDefault(g => g.Id == gameId);
    }

    private static byte[] Malformed(IpcEnvelope request, string message) => Error(request, IpcErrorCode.Malformed, message);

    private static byte[] Error(IpcEnvelope request, string code, string message) =>
        IpcCodec.Encode(IpcMessageType.Error, request.Id, new ErrorAck(code, message));

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}

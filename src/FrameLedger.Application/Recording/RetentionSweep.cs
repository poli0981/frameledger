using FrameLedger.Application.Persistence;
using FrameLedger.Application.Settings;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.Application.Recording;

/// <summary>
/// <c>06_DATA_MODEL</c> §Retention's "on demand" leg (P4 PR-7, Tools ▸ Database maintenance): the rule every finalize
/// applies to its own game — the raw frame and sensor series of all but the newest
/// <c>retention.raw_sessions_per_game</c> sessions go, aggregates and segments stay — applied to every game at once.
/// The blob tables are the Agent's (§Writer ownership), so this runs in the Agent when the App sends
/// <c>SweepRetention</c>, with the setting the Agent reads itself. A setting of 0 is unlimited and sweeps nothing.
/// </summary>
public sealed class RetentionSweep
{
    private readonly ISessionRepository _sessions;
    private readonly RegisteredSettings _settings;

    public RetentionSweep(ISessionRepository sessions, RegisteredSettings settings)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public async ValueTask<SweepRetentionAck> RunAsync(CancellationToken ct = default)
    {
        int keep = await _settings.GetIntegerAsync(SettingsRegistry.RetentionRawSessionsPerGame, ct).ConfigureAwait(false);
        if (keep == 0)
        {
            return new SweepRetentionAck(0, 0, 0);
        }

        RetentionSweepResult result = await _sessions.SweepRetentionAllAsync(keep, ct).ConfigureAwait(false);
        return new SweepRetentionAck(keep, result.Games, result.Sessions);
    }
}

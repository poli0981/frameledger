using FrameLedger.Application.Settings;

namespace FrameLedger.Application.Recording;

/// <summary>
/// The registry's three recording keys applied per session (D16): <c>capture.min_session_s</c>,
/// <c>retention.raw_sessions_per_game</c> (0 = unlimited, which is a sweep that keeps everything, never one
/// that keeps nothing) and <c>telemetry.interval_ms</c>. An operator's bounded capture (a baseline minimum
/// already under the product's) keeps its bound — the setting raises or lowers the product's rule, not the
/// operator's.
/// </summary>
public sealed class SettingsRecorderPolicy : IRecorderPolicy
{
    private readonly RegisteredSettings _settings;

    public SettingsRecorderPolicy(RegisteredSettings settings) => _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    public async ValueTask<RecorderOptions> ResolveAsync(RecorderOptions baseline, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        int minimumSeconds = await _settings.GetIntegerAsync(SettingsRegistry.CaptureMinSessionSeconds, ct).ConfigureAwait(false);
        int keep = await _settings.GetIntegerAsync(SettingsRegistry.RetentionRawSessionsPerGame, ct).ConfigureAwait(false);
        int intervalMs = await _settings.GetIntegerAsync(SettingsRegistry.TelemetryIntervalMs, ct).ConfigureAwait(false);

        TimeSpan minimum = baseline.MinimumSessionLength < SessionFinalizer.MinimumSessionLength
            ? baseline.MinimumSessionLength
            : TimeSpan.FromSeconds(minimumSeconds);
        return baseline with
        {
            MinimumSessionLength = minimum,
            RetentionKeep = keep == 0 ? int.MaxValue : keep,
            TelemetryInterval = TimeSpan.FromMilliseconds(intervalMs),
        };
    }
}

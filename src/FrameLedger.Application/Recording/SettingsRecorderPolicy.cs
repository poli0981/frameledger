// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Input;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Settings;

namespace FrameLedger.Application.Recording;

/// <summary>
/// The registry's three recording keys applied per session (D16): <c>capture.min_session_s</c>,
/// <c>retention.raw_sessions_per_game</c> (0 = unlimited, which is a sweep that keeps everything, never one
/// that keeps nothing) and <c>telemetry.interval_ms</c>. An operator's bounded capture (a baseline minimum
/// already under the product's) keeps its bound — the setting raises or lowers the product's rule, not the
/// operator's.
/// </summary>
/// <remarks>
/// <b>The input reading has a second condition</b> (owner decision D156): <c>capture.input_devices</c> is on unless
/// switched off, and the Agent may be recording before the App has shown the Privacy Policy that describes the
/// reading. So a session reads controllers and keyboard-and-mouse activity only when the accepted Privacy Policy is
/// that version or a later one (<see cref="InputTerms"/>). With no acceptance store given - an operator's console -
/// nothing can be shown to be accepted, and nothing is read.
/// </remarks>
public sealed class SettingsRecorderPolicy : IRecorderPolicy
{
    private readonly RegisteredSettings _settings;
    private readonly ILegalAcceptanceStore? _legal;

    public SettingsRecorderPolicy(RegisteredSettings settings, ILegalAcceptanceStore? legal = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _legal = legal;
    }

    public async ValueTask<RecorderOptions> ResolveAsync(RecorderOptions baseline, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        int minimumSeconds = await _settings.GetIntegerAsync(SettingsRegistry.CaptureMinSessionSeconds, ct).ConfigureAwait(false);
        int keep = await _settings.GetIntegerAsync(SettingsRegistry.RetentionRawSessionsPerGame, ct).ConfigureAwait(false);
        int intervalMs = await _settings.GetIntegerAsync(SettingsRegistry.TelemetryIntervalMs, ct).ConfigureAwait(false);
        bool advanced = await _settings.GetBooleanAsync(SettingsRegistry.TelemetryAdvanced, ct).ConfigureAwait(false);
        bool input = await _settings.GetBooleanAsync(SettingsRegistry.CaptureInputDevices, ct).ConfigureAwait(false);
        bool accepted = input && _legal is not null
            && InputTerms.Accepted((await _legal.FindAsync(InputTerms.PrivacyDocument, ct).ConfigureAwait(false))?.Version);

        TimeSpan minimum = baseline.MinimumSessionLength < SessionFinalizer.MinimumSessionLength
            ? baseline.MinimumSessionLength
            : TimeSpan.FromSeconds(minimumSeconds);
        return baseline with
        {
            MinimumSessionLength = minimum,
            RetentionKeep = keep == 0 ? int.MaxValue : keep,
            TelemetryInterval = TimeSpan.FromMilliseconds(intervalMs),
            AdvancedMetrics = advanced,
            InputDevices = accepted,
            InputHeldForTerms = input && !accepted,
        };
    }
}

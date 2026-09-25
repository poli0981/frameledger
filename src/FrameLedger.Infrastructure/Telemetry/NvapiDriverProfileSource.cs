using FrameLedger.Application.Capture;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// <see cref="IDriverProfileSource"/> over the bridge's read-only <c>FlNvDriverProfile</c> (ABI 2, beta.8): the DRS profile the
/// NVIDIA driver applies to an executable, and the values it gives <see cref="NvidiaDriverSettings.All"/>.
/// </summary>
/// <remarks>
/// From the driver's own settings store, by path: no game process is opened and nothing is loaded into one (CLAUDE.md rule 4),
/// and the bridge calls no DRS setter. Owns one bridge reference for its lifetime; a machine without an NVIDIA driver, or a
/// build without the bridge beside it, answers <see cref="DriverProfileOutcome.Degraded"/> and never throws.
/// </remarks>
public sealed class NvapiDriverProfileSource : IDriverProfileSource, IDisposable
{
    private readonly INvapiBridge _bridge;
    private readonly Lock _lock = new();
    private bool _started;
    private bool _usable;
    private int _refusal;
    private bool _disposed;

    public NvapiDriverProfileSource(INvapiBridge bridge)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    /// <summary>Over the real DLL: the bridge is born owned here.</summary>
    public NvapiDriverProfileSource()
    {
        _bridge = new NativeNvapiBridge();
    }

    public DriverProfileReading Read(string exePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_lock)
        {
            if (!_started)
            {
                _started = true;
                _refusal = _bridge.Init();
                // DRS is per driver, not per GPU handle: a driver that enumerated no GPU still has its settings store.
                _usable = _refusal is 0 or NativeNvapiBridge.NoGpu;
            }

            if (!_usable)
            {
                return new DriverProfileReading { Outcome = DriverProfileOutcome.Degraded, NvapiStatus = _refusal };
            }

            int status = _bridge.DriverProfile(exePath, NvidiaDriverSettings.All, out NvapiDriverProfile profile);
            if (status != 0)
            {
                return new DriverProfileReading { Outcome = DriverProfileOutcome.Degraded, NvapiStatus = status };
            }

            return Map(profile);
        }
    }

    /// <summary>The bridge's answer as the Application sees it: a value only where the driver gave one, and where it came from.</summary>
    public static DriverProfileReading Map(NvapiDriverProfile profile)
    {
        DriverProfileOutcome outcome = profile.Status switch
        {
            NvapiDriverProfile.Application => DriverProfileOutcome.Application,
            NvapiDriverProfile.Global => DriverProfileOutcome.Global,
            _ => DriverProfileOutcome.Degraded,
        };
        if (outcome == DriverProfileOutcome.Degraded)
        {
            return new DriverProfileReading { Outcome = outcome, NvapiStatus = profile.NvapiStatus };
        }

        int count = (int)Math.Min(profile.Count, (uint)(profile.Settings?.Length ?? 0));
        List<DriverSettingReading> settings = new(count);
        for (int i = 0; i < count; i++)
        {
            NvapiProfileSetting s = profile.Settings![i];
            bool read = s.Status == 0;
            settings.Add(new DriverSettingReading(
                s.Id,
                read && s.Dword != 0 ? s.Value : null,
                !read ? DriverSettingLocation.NotSet : s.Location switch
                {
                    0 => DriverSettingLocation.Profile,
                    1 => DriverSettingLocation.Global,
                    2 => DriverSettingLocation.Base,
                    _ => DriverSettingLocation.Default,
                },
                read && s.Predefined != 0));
        }

        return new DriverProfileReading
        {
            Outcome = outcome,
            ProfileName = string.IsNullOrWhiteSpace(profile.ProfileName) ? null : profile.ProfileName,
            NvapiStatus = profile.NvapiStatus,
            Settings = settings,
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_lock)
        {
            if (_usable)
            {
                _bridge.Shutdown();
                _usable = false;
            }
        }

        _bridge.Dispose();
    }
}

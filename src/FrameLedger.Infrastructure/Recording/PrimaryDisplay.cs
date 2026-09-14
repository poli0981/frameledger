using System.Globalization;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;

namespace FrameLedger.Infrastructure.Recording;

/// <summary>
/// The primary display's current mode for <c>hardware_snapshots.display_res</c> / <c>display_hz</c> (the two
/// columns that were "null until P3"): <c>EnumDisplaySettingsW(NULL, ENUM_CURRENT_SETTINGS)</c>, the documented
/// query, read once per session start. A refusal is a snapshot with no display — never a session that cannot start.
/// </summary>
public static class PrimaryDisplay
{
    /// <summary>The mode as <c>"2560x1440"</c> and <c>144</c>, or nulls when the call fails or the mode is degenerate.</summary>
    public static unsafe (string? Resolution, double? Hz) Read()
    {
        var mode = new DEVMODEW { dmSize = (ushort)sizeof(DEVMODEW) };
        if (!PInvoke.EnumDisplaySettings(null, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, &mode))
        {
            return (null, null);
        }

        if (mode.dmPelsWidth == 0 || mode.dmPelsHeight == 0)
        {
            return (null, null);
        }

        string resolution = string.Create(CultureInfo.InvariantCulture, $"{mode.dmPelsWidth}x{mode.dmPelsHeight}");
        // 0 and 1 mean "the hardware default" per the DEVMODEW contract, which is not a number to store.
        double? hz = mode.dmDisplayFrequency > 1 ? mode.dmDisplayFrequency : null;
        return (resolution, hz);
    }
}

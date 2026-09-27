namespace FrameLedger.Domain.Display;

/// <summary>One sample's classification and the sizes it was made from; null sizes were not read.</summary>
/// <param name="Mode">How the game was being shown.</param>
/// <param name="Source">A <see cref="DisplaySource"/> id, or null when nothing was read (<see cref="DisplayMode.NoWindow"/>).</param>
/// <param name="Client">The window's client area, when a window was read.</param>
/// <param name="BufferWidth">The swap chain's back buffer, when the injected component described one.</param>
/// <param name="BufferHeight">Its height.</param>
/// <param name="Monitor">The monitor the window was on.</param>
/// <param name="MonitorHz">That monitor's refresh rate.</param>
/// <param name="SwapEffect">The swap chain's swap effect name, when described.</param>
public readonly record struct DisplayObservation(
    DisplayMode Mode,
    string? Source,
    ScreenRect? Client,
    uint? BufferWidth,
    uint? BufferHeight,
    ScreenRect? Monitor,
    int? MonitorHz,
    string? SwapEffect);

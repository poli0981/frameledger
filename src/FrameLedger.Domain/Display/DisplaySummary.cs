namespace FrameLedger.Domain.Display;

/// <summary>
/// A session's display facts (<c>sessions.display_*</c>, schema 0016): how long each mode lasted, how often the mode
/// changed, what the facts rested on, and the sizes seen in the mode that lasted longest. Both tiers have one.
/// </summary>
public sealed record DisplaySummary
{
    public long ExclusiveMs { get; init; }

    public long BorderlessMs { get; init; }

    public long WindowedMs { get; init; }

    /// <summary>Time the window covered its monitor and nothing could say whether it was exclusive.</summary>
    public long CoversMs { get; init; }

    public long MinimizedMs { get; init; }

    /// <summary>Time no window of the game could be read — not a mode, and outside every share.</summary>
    public long NoWindowMs { get; init; }

    /// <summary>How many times the mode differed from the sample before (not counting samples with no window).</summary>
    public int Changes { get; init; }

    /// <summary>The strongest <see cref="DisplaySource"/> any sample had; null when no window was ever read.</summary>
    public string? Source { get; init; }

    /// <summary>The window's client size in the longest-lasting shown mode.</summary>
    public int? WindowWidth { get; init; }

    public int? WindowHeight { get; init; }

    /// <summary>The swap chain's back buffer in that mode, when the injected component described one.</summary>
    public uint? BufferWidth { get; init; }

    public uint? BufferHeight { get; init; }

    /// <summary>The monitor the window was on in that mode.</summary>
    public int? MonitorWidth { get; init; }

    public int? MonitorHeight { get; init; }

    public int? MonitorHz { get; init; }

    /// <summary>The swap chain's swap effect name, as last described.</summary>
    public string? SwapEffect { get; init; }

    /// <summary>The time a window was read in any shown or minimised mode: the denominator of every share.</summary>
    public long ObservedMs => ExclusiveMs + BorderlessMs + WindowedMs + CoversMs + MinimizedMs;

    /// <summary>Whether exclusive fullscreen and borderless can be told apart for the whole observed time.</summary>
    public bool ExclusivityKnown => CoversMs == 0 && ObservedMs > 0;

    /// <summary>The share of observed time in <paramref name="mode"/>, 0–100; null when nothing was observed.</summary>
    public double? SharePercent(DisplayMode mode)
    {
        long observed = ObservedMs;
        if (observed <= 0)
        {
            return null;
        }

        long ms = mode switch
        {
            DisplayMode.ExclusiveFullscreen => ExclusiveMs,
            DisplayMode.Borderless => BorderlessMs,
            DisplayMode.Windowed => WindowedMs,
            DisplayMode.CoversScreen => CoversMs,
            DisplayMode.Minimized => MinimizedMs,
            _ => 0,
        };
        return 100.0 * ms / observed;
    }

    /// <summary>The shown mode (not minimised) that lasted longest, or null when none was observed.</summary>
    public DisplayMode? Dominant
    {
        get
        {
            (DisplayMode Mode, long Ms)[] shown =
            [
                (DisplayMode.ExclusiveFullscreen, ExclusiveMs),
                (DisplayMode.Borderless, BorderlessMs),
                (DisplayMode.CoversScreen, CoversMs),
                (DisplayMode.Windowed, WindowedMs),
            ];
            (DisplayMode mode, long ms) = shown.MaxBy(static s => s.Ms);
            return ms > 0 ? mode : null;
        }
    }
}

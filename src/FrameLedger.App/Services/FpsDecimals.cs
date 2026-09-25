namespace FrameLedger.App.Services;

/// <summary>
/// <c>ui.fps_decimals</c> as the formatters read it (beta.8, owner request 2026-09-25): whether every FPS figure shows two
/// decimals ("62.40") or a whole number ("62"). One process-wide value like <see cref="LoggingLevel"/>: set from the settings
/// row at start and by the Settings page when it changes; a page shows it the next time it formats a number.
/// </summary>
public static class FpsDecimals
{
    private static volatile bool _two;

    /// <summary>True: two decimals, always ("62.40"); false: a whole number.</summary>
    public static bool Two
    {
        get => _two;
        set => _two = value;
    }
}

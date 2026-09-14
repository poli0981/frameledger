using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Services;

/// <summary>
/// The two appearance keys of the settings registry (HANDOFF §P3 decision D16): <c>ui.theme</c> —
/// <c>system</c> · <c>light</c> · <c>dark</c> — and <c>ui.language</c> — <c>en</c> · <c>vi</c> · <c>ja</c>.
/// Read once at startup (before any window, because resources are read at XAML load), written by the Settings
/// page. The App owns <c>settings</c> under <c>06_DATA_MODEL</c> §Writer ownership.
/// </summary>
public sealed class AppearanceSettings(ISettingsStore settings)
{
    public const string ThemeKey = "ui.theme";
    public const string LanguageKey = "ui.language";

    public static IReadOnlyList<string> Languages { get; } = ["en", "vi", "ja"];

    public AppTheme Theme { get; private set; } = AppTheme.System;

    public string Language { get; private set; } = "en";

    public async Task LoadAsync(CancellationToken ct = default)
    {
        Theme = ParseTheme(await settings.GetAsync(ThemeKey, ct).ConfigureAwait(false));
        Language = ParseLanguage(await settings.GetAsync(LanguageKey, ct).ConfigureAwait(false));
    }

    public async Task SetThemeAsync(AppTheme theme, CancellationToken ct = default)
    {
        Theme = theme;
        await settings.SetAsync(ThemeKey, ThemeText(theme), ct).ConfigureAwait(false);
    }

    public async Task SetLanguageAsync(string language, CancellationToken ct = default)
    {
        Language = ParseLanguage(language);
        await settings.SetAsync(LanguageKey, Language, ct).ConfigureAwait(false);
    }

    /// <summary>An unknown or absent value is the default, never an exception: a hand-edited row must not stop the App.</summary>
    public static AppTheme ParseTheme(string? value) => value switch
    {
        "light" => AppTheme.Light,
        "dark" => AppTheme.Dark,
        _ => AppTheme.System,
    };

    public static string ThemeText(AppTheme theme) => theme switch
    {
        AppTheme.Light => "light",
        AppTheme.Dark => "dark",
        _ => "system",
    };

    public static string ParseLanguage(string? value) =>
        value is not null && Languages.Contains(value, StringComparer.Ordinal) ? value : "en";
}

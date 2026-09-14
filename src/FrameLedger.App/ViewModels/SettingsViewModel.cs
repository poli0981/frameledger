using CommunityToolkit.Mvvm.ComponentModel;
using FrameLedger.App.Services;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// Settings, the part this build has (08_UI §Settings — Appearance): theme applies at once and persists;
/// language persists and rebuilds the shell in the new culture (09_I18N §Mechanics).
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppearanceSettings _appearance;
    private readonly IThemeApplier _themes;
    private readonly ShellHost _shell;
    private readonly bool _loading = true;

    [ObservableProperty]
    private AppTheme _selectedTheme;

    [ObservableProperty]
    private string _selectedLanguage = "en";

    public SettingsViewModel(AppearanceSettings appearance, IThemeApplier theme, ShellHost shell)
    {
        _appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
        _themes = theme ?? throw new ArgumentNullException(nameof(theme));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        SelectedTheme = appearance.Theme;
        SelectedLanguage = appearance.Language;
        _loading = false;
    }

    public static string Header => Strings.Settings_Header;

    public static string AppearanceHeader => Strings.Settings_Appearance_Header;

    public static string ThemeLabel => Strings.Settings_Theme_Label;

    public static string LanguageLabel => Strings.Settings_Language_Label;

    public static string LanguageNote => Strings.Settings_Language_Note;

    public static IReadOnlyList<Choice<AppTheme>> Themes { get; } =
    [
        new(AppTheme.System, Strings.Settings_Theme_System),
        new(AppTheme.Light, Strings.Settings_Theme_Light),
        new(AppTheme.Dark, Strings.Settings_Theme_Dark),
    ];

    public static IReadOnlyList<Choice<string>> Languages { get; } =
    [
        new("en", "English"),
        new("vi", "Tiếng Việt"),
        new("ja", "日本語"),
    ];

    /// <summary>A pending write, for a test to await; the UI does not.</summary>
    public Task Pending { get; private set; } = Task.CompletedTask;

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        if (_loading)
        {
            return;
        }

        _themes.Apply(value, _shell.Current);
        Pending = _appearance.SetThemeAsync(value);
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        if (_loading || string.Equals(value, _appearance.Language, StringComparison.Ordinal))
        {
            return;
        }

        Pending = ApplyLanguageAsync(value);
    }

    private async Task ApplyLanguageAsync(string value)
    {
        await _appearance.SetLanguageAsync(value).ConfigureAwait(true);
        App.ApplyCulture(_appearance.Language);
        _shell.Rebuild();
    }
}

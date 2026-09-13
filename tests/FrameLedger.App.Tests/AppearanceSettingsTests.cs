using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Tests;

/// <summary>The two appearance keys (settings registry, D16): tolerant reads, exact writes.</summary>
public sealed class AppearanceSettingsTests
{
    private sealed class FakeSettings : ISettingsStore
    {
        public Dictionary<string, string> Rows { get; } = new(StringComparer.Ordinal);

        public ValueTask<string?> GetAsync(string key, CancellationToken ct = default) => ValueTask.FromResult(Rows.GetValueOrDefault(key));

        public ValueTask SetAsync(string key, string value, CancellationToken ct = default)
        {
            Rows[key] = value;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task NoRowsMeansSystemThemeAndEnglish()
    {
        var settings = new AppearanceSettings(new FakeSettings());
        await settings.LoadAsync(TestContext.Current.CancellationToken);
        settings.Theme.Should().Be(AppTheme.System);
        settings.Language.Should().Be("en");
    }

    [Fact]
    public async Task AHandEditedUnknownValueIsTheDefaultNotAnException()
    {
        var store = new FakeSettings();
        store.Rows[AppearanceSettings.ThemeKey] = "purple";
        store.Rows[AppearanceSettings.LanguageKey] = "klingon";
        var settings = new AppearanceSettings(store);
        await settings.LoadAsync(TestContext.Current.CancellationToken);
        settings.Theme.Should().Be(AppTheme.System);
        settings.Language.Should().Be("en");
    }

    [Fact]
    public async Task WritesLandUnderTheRegistrysKeysAndReadBack()
    {
        var store = new FakeSettings();
        var settings = new AppearanceSettings(store);
        await settings.SetThemeAsync(AppTheme.Dark, TestContext.Current.CancellationToken);
        await settings.SetLanguageAsync("ja", TestContext.Current.CancellationToken);

        store.Rows.Should().Contain(AppearanceSettings.ThemeKey, "dark").And.Contain(AppearanceSettings.LanguageKey, "ja");

        var again = new AppearanceSettings(store);
        await again.LoadAsync(TestContext.Current.CancellationToken);
        again.Theme.Should().Be(AppTheme.Dark);
        again.Language.Should().Be("ja");
    }

    [Theory]
    [InlineData(AppTheme.System, "system")]
    [InlineData(AppTheme.Light, "light")]
    [InlineData(AppTheme.Dark, "dark")]
    public void ThemeTextRoundTrips(AppTheme theme, string text)
    {
        AppearanceSettings.ThemeText(theme).Should().Be(text);
        AppearanceSettings.ParseTheme(text).Should().Be(theme);
    }
}

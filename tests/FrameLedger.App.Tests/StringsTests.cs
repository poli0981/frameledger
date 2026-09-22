using System.Globalization;
using System.IO;
using System.Xml.Linq;
using FluentAssertions;

namespace FrameLedger.App.Tests;

/// <summary>
/// The generated accessor against the satellites the build produced: every key resolves in en, vi and ja
/// (a missing satellite would fall back to English silently, and this is the one place that would show), and
/// the accessor's key list is the resx's — the audit gate checks the same from the outside.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class StringsTests
{
    private static readonly string[] _cultures = ["en", "vi", "ja"];

    [Theory]
    [InlineData("en")]
    [InlineData("vi")]
    [InlineData("ja")]
    public void EveryKeyResolvesToANonEmptyStringIn(string culture)
    {
        var ci = CultureInfo.GetCultureInfo(culture);
        foreach (string key in Strings.Keys)
        {
            Strings.ResourceManager.GetString(key, ci).Should().NotBeNullOrWhiteSpace($"{key} in {culture}");
        }
    }

    [Fact]
    public void TheSatellitesAreReallyLoadedNotTheEnglishFallback()
    {
        string en = Strings.ResourceManager.GetString(nameof(Strings.Nav_Settings), CultureInfo.GetCultureInfo("en"))!;
        string vi = Strings.ResourceManager.GetString(nameof(Strings.Nav_Settings), CultureInfo.GetCultureInfo("vi"))!;
        string ja = Strings.ResourceManager.GetString(nameof(Strings.Nav_Settings), CultureInfo.GetCultureInfo("ja"))!;
        vi.Should().NotBe(en, "vi/FrameLedger.resources.dll must be beside the assembly");
        ja.Should().NotBe(en, "ja/FrameLedger.resources.dll must be beside the assembly");
        _cultures.Should().HaveCount(3);
    }

    [Fact]
    public void TheAccessorsKeysAreTheResxsKeys()
    {
        string resx = Path.Combine(RepoRoot(), "src", "FrameLedger.App", "Strings.resx");
        string[] keys = XDocument.Load(resx).Root!.Elements("data").Select(d => (string)d.Attribute("name")!).ToArray();
        Strings.Keys.Should().BeEquivalentTo(keys, "Strings.Designer.cs is generated from Strings.resx and committed; tools/resx-audit.ps1 fails on drift");
    }

    [Fact]
    public void TheCulturePropertyOverridesTheThread()
    {
        CultureInfo? previous = Strings.Culture;
        try
        {
            Strings.Culture = CultureInfo.GetCultureInfo("vi");
            Strings.Nav_Settings.Should().Be("Cài đặt");
            Strings.Culture = CultureInfo.GetCultureInfo("en");
            Strings.Nav_Settings.Should().Be("Settings");
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    /// <summary>
    /// The owner's beta.3 screenshot: a Vietnamese UI whose consent dialog and refusal notices were
    /// English. FrameLedger.Shared's Strings had no culture of its own, and the thread culture set during the async
    /// start does not reach the dispatcher's later operations. <c>ApplyCulture</c> sets BOTH families explicitly.
    /// </summary>
    [Fact]
    public void ApplyingTheUiLanguageSetsTheSafetyFamilyToo()
    {
        CultureInfo? app = Strings.Culture;
        CultureInfo? safety = Shared.Strings.Culture;
        CultureInfo thread = CultureInfo.CurrentUICulture;
        CultureInfo? threadDefault = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            App.ApplyCulture("vi");
            // The thread's culture is put back to English ON PURPOSE: the explicit culture must not depend on it.
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

            Shared.Strings.Culture.Should().Be(CultureInfo.GetCultureInfo("vi"));
            Shared.Strings.Safety_HookingTurnedOff.Should().StartWith("Hooking đã được tắt");
            Shared.Strings.Safety_Blocked_Toggle_Format.Should().StartWith("Hooking bị vô hiệu hoá");
            Strings.Nav_Settings.Should().Be("Cài đặt");
        }
        finally
        {
            Strings.Culture = app;
            Shared.Strings.Culture = safety;
            CultureInfo.CurrentUICulture = thread;
            CultureInfo.DefaultThreadCurrentUICulture = threadDefault;
        }
    }

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "FrameLedger.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("FrameLedger.slnx not found above the test binary");
    }
}

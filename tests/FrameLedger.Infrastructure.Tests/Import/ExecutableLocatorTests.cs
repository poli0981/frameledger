using FluentAssertions;
using FrameLedger.Infrastructure.Import;

namespace FrameLedger.Infrastructure.Tests.Import;

/// <summary>
/// The install trees the locator was measured wrong on (2026-09-21), rebuilt with the real file names and the real
/// sizes: what mattered on GIRLS' FRONTLINE 2 was that a plugin-folder helper out-weighed a Unity player stub.
/// </summary>
public sealed class ExecutableLocatorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-locator-" + Guid.NewGuid().ToString("N"));

    public ExecutableLocatorTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Sub(params string[] parts)
    {
        string p = Path.Combine([_dir, .. parts]);
        Directory.CreateDirectory(p);
        return p;
    }

    [Fact]
    public async Task AUnityPlayerBeatsTheLargerBrowserHelperInItsPluginFolder()
    {
        // An apostrophe in the directory name, as on disk: the path is data everywhere it travels.
        string game = Sub("GIRLS' FRONTLINE 2 EXILIUM");
        string plugins = Sub("GIRLS' FRONTLINE 2 EXILIUM", "GF2_Exilium_Data", "Plugins");
        await File.WriteAllBytesAsync(Path.Combine(game, "GF2_Exilium.exe"), new byte[675_392], Ct);
        await File.WriteAllBytesAsync(Path.Combine(game, "UnityCrashHandler64.exe"), new byte[1_077_896], Ct);
        await File.WriteAllBytesAsync(Path.Combine(game, "crashpad_handler.exe"), new byte[917_056], Ct);
        await File.WriteAllBytesAsync(Path.Combine(game, "KernelDumpAnalyzer.exe"), new byte[36_928], Ct);
        await File.WriteAllBytesAsync(Path.Combine(plugins, "ZFGameBrowser.exe"), new byte[1_082_944], Ct);

        new ExecutableLocator().PickPrimary(game).Should().Be(Path.Combine(game, "GF2_Exilium.exe"),
            "X.exe beside X_Data is the Unity player; the browser helper is larger, is under X_Data\\Plugins, and is not the game");
    }

    [Fact]
    public async Task ThePluginFolderIsExcludedEvenWhenNothingElseMarksTheHelper()
    {
        string game = Sub("Title");
        string plugins = Sub("Title", "Title_Data", "Plugins", "x86_64");
        await File.WriteAllBytesAsync(Path.Combine(game, "Title.exe"), new byte[100], Ct);
        await File.WriteAllBytesAsync(Path.Combine(plugins, "Overlay.exe"), new byte[900_000], Ct);

        ExecutableLocator.IsUnderPluginFolder(game, Path.Combine(plugins, "Overlay.exe")).Should().BeTrue();
        ExecutableLocator.IsUnderPluginFolder(game, Path.Combine(game, "Title.exe")).Should().BeFalse();
        new ExecutableLocator().PickPrimary(game).Should().Be(Path.Combine(game, "Title.exe"));
    }

    [Fact]
    public async Task AnUnrealShippingBinaryBeatsALargerRootBootstrapper()
    {
        string game = Sub("Rune Factory Guardians of Azuma");
        string bin = Sub("Rune Factory Guardians of Azuma", "Game", "Binaries", "Win64");
        await File.WriteAllBytesAsync(Path.Combine(game, "Game.exe"), new byte[500_000], Ct);
        await File.WriteAllBytesAsync(Path.Combine(bin, "Game-Win64-Shipping.exe"), new byte[400_000], Ct);

        new ExecutableLocator().PickPrimary(game).Should().Be(Path.Combine(bin, "Game-Win64-Shipping.exe"),
            "the root executable of an Unreal package starts the shipping binary and exits; the shipping binary is the process that presents");
    }

    [Fact]
    public async Task AnExecutableNamedAfterTheInstallBeatsALargerStranger()
    {
        string game = Sub("Cyberpunk 2077");
        string bin = Sub("Cyberpunk 2077", "bin", "x64");
        await File.WriteAllBytesAsync(Path.Combine(bin, "Cyberpunk2077.exe"), new byte[1000], Ct);
        await File.WriteAllBytesAsync(Path.Combine(bin, "REDprelauncher.exe"), new byte[5000], Ct);

        new ExecutableLocator().PickPrimary(game).Should().Be(Path.Combine(bin, "Cyberpunk2077.exe"));
    }

    [Fact]
    public async Task ARedistributablesFolderIsNotWalkedAndTheFourthLevelIs()
    {
        string rdr = Sub("Red Dead Redemption 2");
        string redist = Sub("Red Dead Redemption 2", "Redistributables");
        await File.WriteAllBytesAsync(Path.Combine(rdr, "RDR2.exe"), new byte[90_000], Ct);
        await File.WriteAllBytesAsync(Path.Combine(rdr, "PlayRDR2.exe"), new byte[500], Ct);
        await File.WriteAllBytesAsync(Path.Combine(redist, "Rockstar-Games-Launcher.exe"), new byte[140_000], Ct);
        new ExecutableLocator().PickPrimary(rdr).Should().Be(Path.Combine(rdr, "RDR2.exe"));

        string beast = Sub("Dying Light The Beast");
        string bin = Sub("Dying Light The Beast", "ph_ft", "work", "bin", "x64");
        await File.WriteAllBytesAsync(Path.Combine(bin, "DyingLightGame_TheBeast_x64_rwdi.exe"), new byte[1000], Ct);
        await File.WriteAllBytesAsync(Path.Combine(bin, "crashpad_handler.exe"), new byte[5000], Ct);
        new ExecutableLocator().PickPrimary(beast).Should().Be(Path.Combine(bin, "DyingLightGame_TheBeast_x64_rwdi.exe"), "the title keeps its game four levels down");
    }

    [Fact]
    public async Task SizeStillDecidesBetweenEquals()
    {
        string game = Sub("Some Title");
        await File.WriteAllBytesAsync(Path.Combine(game, "a.exe"), new byte[10], Ct);
        await File.WriteAllBytesAsync(Path.Combine(game, "b.exe"), new byte[2000], Ct);

        new ExecutableLocator().PickPrimary(game).Should().Be(Path.Combine(game, "b.exe"));
    }

    [Theory]
    [InlineData("ZFGameBrowser")]
    [InlineData("KernelDumpAnalyzer")]
    [InlineData("msedgewebview2")]
    [InlineData("QtWebEngineSubprocess")]
    [InlineData("vconsole2")]
    public void TheHelpersThisWasMeasuredOnAreHelpers(string name) =>
        ExecutableLocator.IsHelper(name).Should().BeTrue();
}

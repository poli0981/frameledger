using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using FrameLedger.App.Charts;

namespace FrameLedger.App.Tests;

/// <summary>The two palette dictionaries carry exactly the keys the record reads, as colours — a key missing from one theme would throw at the first chart under that theme.</summary>
public sealed class ChartPaletteTests
{
    private static readonly XNamespace _x = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace _p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Theory]
    [InlineData("ChartPalette.Dark.xaml")]
    [InlineData("ChartPalette.Light.xaml")]
    public void ADictionaryCarriesEveryKeyOnceAsAColor(string file)
    {
        string path = Path.Combine(RepoRoot(), "src", "FrameLedger.App", "Styles", file);
        XDocument doc = XDocument.Load(path);
        var keys = doc.Root!.Elements(_p + "Color").Select(e => (string)e.Attribute(_x + "Key")!).ToList();
        keys.Should().OnlyHaveUniqueItems();
        keys.Should().BeEquivalentTo(ChartPalette.Keys, $"{file} must define exactly what ChartPalette reads");
        foreach (XElement color in doc.Root.Elements(_p + "Color"))
        {
            color.Value.Should().MatchRegex("^#[0-9A-Fa-f]{8}$", "ARGB hex, so the alpha is explicit");
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

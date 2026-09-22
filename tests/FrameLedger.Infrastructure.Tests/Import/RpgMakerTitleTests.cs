using FluentAssertions;
using FrameLedger.Infrastructure.Import;

namespace FrameLedger.Infrastructure.Tests.Import;

/// <summary>RPG Maker's own title for a hand-added <c>Game.exe</c> (2026-09-23), read from real files in a scratch tree.</summary>
public sealed class RpgMakerTitleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "fl-rpgtitle-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string Write(string relative, string content)
    {
        string path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void AnMvTitleBesideTheExecutableIsRead()
    {
        string exe = Write(@"HHW\swiftshader\Game.exe", "");
        Write(@"HHW\swiftshader\www\data\System.json", "{\"gameTitle\":\"HELLO, HELLO WORLD!\",\"locale\":\"en_US\"}");

        RpgMakerTitle.TryRead(exe).Should().Be("HELLO, HELLO WORLD!");
    }

    [Fact]
    public void AnMzTitleAFolderAboveIsRead()
    {
        string exe = Write(@"MZ\runtime\Game.exe", "");
        Write(@"MZ\data\System.json", "{\"gameTitle\":\"  An MZ Game  \"}");

        RpgMakerTitle.TryRead(exe).Should().Be("An MZ Game");
    }

    [Theory]
    [InlineData("{\"locale\":\"en_US\"}")]
    [InlineData("{\"gameTitle\":\"\"}")]
    [InlineData("{\"gameTitle\":42}")]
    [InlineData("not json")]
    [InlineData("[]")]
    public void NoUsableTitleIsNull(string content)
    {
        string exe = Write(@"Bad\Game.exe", "");
        Write(@"Bad\www\data\System.json", content);

        RpgMakerTitle.TryRead(exe).Should().BeNull();
    }

    [Fact]
    public void NoFileIsNull()
    {
        RpgMakerTitle.TryRead(Write(@"None\Game.exe", "")).Should().BeNull();
    }
}

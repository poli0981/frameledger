using FluentAssertions;
using FrameLedger.Application.Import;

namespace FrameLedger.Application.Tests.Import;

/// <summary>The name a hand-added executable gets (2026-09-23): its own when it names something, else the game's.</summary>
public sealed class GameTitleGuessTests
{
    [Theory]
    [InlineData(@"D:\SteamLibrary\steamapps\common\DARK SOULS III\Game\DarkSoulsIII.exe", "DarkSoulsIII")]
    [InlineData(@"D:\SteamLibrary\steamapps\common\ELDEN RING\Game\eldenring.exe", "eldenring")]
    [InlineData(@"D:\another\it\hello-hello-world\HELLO, HELLO WORLD!\swiftshader\Game.exe", "HELLO, HELLO WORLD!")]
    [InlineData(@"D:\another\it\996IR version 1.2.2 (Windows)\996ir\Game.exe", "996ir")]
    [InlineData(@"C:\Games\Some Title\bin\x64\launcher.exe", "Some Title")]
    [InlineData(@"C:\Game.exe", "Game")]
    public void AGenericExecutableIsNamedByTheNearestFolderThatIsNotARuntimeFolder(string exe, string expected)
    {
        GameTitleGuess.Guess(exe).Should().Be(expected);
    }

    [Fact]
    public void TheEngineTitleWinsForAGenericNameAndIsNotAskedForOne()
    {
        int asked = 0;
        GameTitleGuess.Guess(@"D:\another\it\hello-hello-world\HELLO, HELLO WORLD!\swiftshader\Game.exe", () =>
        {
            asked++;
            return "  HELLO, HELLO WORLD!  ";
        }).Should().Be("HELLO, HELLO WORLD!");

        GameTitleGuess.Guess(@"C:\Games\Title\Title.exe", () =>
        {
            asked++;
            return "never";
        }).Should().Be("Title");

        asked.Should().Be(1, "the engine title is read only when the file name says nothing");
        GameTitleGuess.Guess(@"C:\Games\Title\Game.exe", static () => "   ").Should().Be("Title", "a blank title is no title");
    }
}

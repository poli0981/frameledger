using FluentAssertions;
using FrameLedger.Infrastructure.Import;

namespace FrameLedger.Infrastructure.Tests.Import;

/// <summary>Valve's KeyValues text (P4 PR-4): the two Steam files' shapes, comments, escapes, case-insensitive keys, and malformed input that costs an answer rather than throwing.</summary>
public sealed class ValveKeyValuesTests
{
    [Fact]
    public void AnAppManifestReadsBackItsFields()
    {
        const string acf = """
            "AppState"
            {
            	"appid"		"1091500"
            	"name"		"Cyberpunk 2077"
            	"installdir"		"Cyberpunk 2077"
            	"buildid"		"15877371"   // a comment after the value
            	"UserConfig"
            	{
            		"language"		"english"
            	}
            }
            """;
        KeyValuesBlock root = ValveKeyValues.Parse(acf);
        KeyValuesBlock state = root.Child("appstate")!;
        state.Should().NotBeNull("keys compare case-insensitively");
        state.Value("appid").Should().Be("1091500");
        state.Value("name").Should().Be("Cyberpunk 2077");
        state.Value("installdir").Should().Be("Cyberpunk 2077");
        state.Value("buildid").Should().Be("15877371");
        state.Child("UserConfig")!.Value("language").Should().Be("english");
    }

    [Fact]
    public void LibraryFoldersListsEveryPathAndEscapesAreHonoured()
    {
        const string vdf = """
            "libraryfolders"
            {
            	"0"
            	{
            		"path"		"C:\\Program Files (x86)\\Steam"
            		"apps" { "228980" "1234" }
            	}
            	"1"
            	{
            		"path"		"D:\\SteamLibrary"
            		"label"		"say \"hi\""
            	}
            }
            """;
        KeyValuesBlock folders = ValveKeyValues.Parse(vdf).Child("libraryfolders")!;
        folders.Children.Select(static c => c.Value.Value("path")).Should().Equal(@"C:\Program Files (x86)\Steam", @"D:\SteamLibrary");
        folders.Child("1")!.Value("label").Should().Be("say \"hi\"");
        folders.Child("0")!.Child("apps")!.Value("228980").Should().Be("1234", "bare and quoted tokens both parse");
    }

    [Fact]
    public void MalformedInputCostsAnAnswerNotAnException()
    {
        ValveKeyValues.Parse("\"AppState\" { \"appid\" \"1\" ").Child("AppState")!.Value("appid").Should().Be("1", "an unterminated block keeps what parsed");
        ValveKeyValues.Parse("\"key\"").Value("key").Should().BeNull("a key with no value is dropped");
        ValveKeyValues.Parse("}}}{{{").Entries.Should().BeEmpty();
        ValveKeyValues.Parse(string.Empty).Entries.Should().BeEmpty();
        ValveKeyValues.Parse("\"unterminated").Entries.Should().BeEmpty();
    }
}

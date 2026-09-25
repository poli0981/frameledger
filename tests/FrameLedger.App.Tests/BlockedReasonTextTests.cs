using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>
/// <c>games.hook_blocked_reason</c> in the user's language (beta.8): the structured <c>Reason|Family|Signal</c> the Agent
/// writes since 2026-09-25 reads as a sentence naming the anti-cheat and what of it was found; a reason this build has no
/// sentence for still names both; a row written before then, which cannot be split back apart, is shown as it was stored.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class BlockedReasonTextTests
{
    private static readonly string[] _cultures = ["en", "vi", "ja"];

    [Theory]
    [InlineData("BlockedModule|BattlEye|BEClient_x64.dll", "BattlEye — its module BEClient_x64.dll was loaded in the game")]
    [InlineData("AntiCheatDirectory|Easy Anti-Cheat|EasyAntiCheat", "Easy Anti-Cheat — its folder EasyAntiCheat ships with the game")]
    [InlineData("AntiCheatFile|Kernel driver in the game folder|guard64.sys", "Kernel driver in the game folder — guard64.sys ships with the game")]
    [InlineData("BlockedExecutable|Valve Anti-Cheat|cs2.exe", "Valve Anti-Cheat — cs2.exe is a listed title")]
    [InlineData("BlockedStoreId|Valve Anti-Cheat|steam:730", "Valve Anti-Cheat — the game's store id steam:730 is listed")]
    [InlineData("SomethingLater|A family|a signal", "A family (a signal)")]
    public void TheStructuredFormReadsAsASentence(string stored, string expected)
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            BlockedReasonText.Describe(stored).Should().Be(expected);
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    [Fact]
    public void AnEmptyPartReadsAsNotAvailable()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            BlockedReasonText.Describe("BlockedModule||x.dll").Should().Be(Strings.Common_NotAvailable + " — its module x.dll was loaded in the game");
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    [Theory]
    [InlineData("BlockedModule: BattlEye BEClient_x64.dll")]
    [InlineData("eac: EasyAntiCheat_EOS.dll")]
    [InlineData("blocked")]
    [InlineData("a|b")]
    public void ARowWrittenBeforeTheStructuredFormIsShownAsStored(string stored) =>
        BlockedReasonText.Describe(stored).Should().Be(stored);

    [Fact]
    public void EveryLanguageHasTheSentences()
    {
        CultureInfo? previous = Strings.Culture;
        try
        {
            foreach (string culture in _cultures)
            {
                Strings.Culture = CultureInfo.GetCultureInfo(culture);
                string text = BlockedReasonText.Describe("AntiCheatDirectory|Easy Anti-Cheat|EasyAntiCheat");
                text.Should().Contain("Easy Anti-Cheat").And.Contain("EasyAntiCheat", culture);
                text.Should().NotContain("{", culture);
            }
        }
        finally
        {
            Strings.Culture = previous;
        }
    }
}

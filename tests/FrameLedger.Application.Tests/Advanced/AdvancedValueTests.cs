// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Advanced;

namespace FrameLedger.Application.Tests.Advanced;

/// <summary>
/// An advanced fact's value has exactly one shape, and no shape means "unknown": a fact that was not read is absent.
/// </summary>
public sealed class AdvancedValueTests
{
    [Fact]
    public void ANumberIsANumberAndNothingElse()
    {
        AdvancedValue v = AdvancedValue.Of(84);

        v.Number.Should().Be(84);
        v.Text.Should().BeNull();
        v.Tokens.Should().BeNull();
        v.Rows.Should().BeNull();
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ANumberThatIsNotOneIsRefused(double value)
        => FluentActions.Invoking(() => AdvancedValue.Of(value)).Should().Throw<ArgumentOutOfRangeException>("a fact that was not read is absent, not NaN");

    [Fact]
    public void AFlagIsOneOrZero()
    {
        AdvancedValue.Of(true).Number.Should().Be(1);
        AdvancedValue.Of(false).Number.Should().Be(0, "a flag that is off was read, and is not an absence");
    }

    [Fact]
    public void TheVendorsWordsAreKeptAsTheyAre()
    {
        AdvancedValue v = AdvancedValue.OfText("98.03.6c.00.68");

        v.Text.Should().Be("98.03.6c.00.68");
        v.Number.Should().BeNull();
        v.Tokens.Should().BeNull();
        FluentActions.Invoking(static () => AdvancedValue.OfText(" ")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(static () => AdvancedValue.OfText(null!)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OurOwnWordsAreTokensInTheOrderGivenAndMayBeNone()
    {
        AdvancedValue.OfTokens("hdr10", "dolby_vision").Tokens.Should().Equal("hdr10", "dolby_vision");
        AdvancedValue.OfTokens().Tokens.Should().BeEmpty("the source answered, and named nothing");
        AdvancedValue.OfTokens("hdr10").Text.Should().BeNull();
    }

    [Fact]
    public void TheTokensAreCopiedSoTheCallersListCannotChangeAStoredValue()
    {
        var mine = new List<string> { "discrete" };

        AdvancedValue v = AdvancedValue.OfTokens(mine);
        mine.Add("integrated");

        v.Tokens.Should().Equal("discrete");
    }

    [Theory]
    [InlineData("Discrete")]
    [InlineData("pci express")]
    [InlineData("pci-express")]
    [InlineData("1st")]
    [InlineData("")]
    public void AWordThatIsNotATokenIsRefused(string word)
    {
        AdvancedValue.IsToken(word).Should().BeFalse();
        FluentActions.Invoking(() => AdvancedValue.OfTokens(word)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ATokenIsLowerCaseLettersDigitsAndUnderscoresStartingWithALetter()
    {
        AdvancedValue.IsToken("hdr10_plus_gaming").Should().BeTrue();
        AdvancedValue.IsToken("_x").Should().BeFalse();
        AdvancedValue.IsToken(null).Should().BeFalse();
        FluentActions.Invoking(static () => AdvancedValue.OfTokens(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ATableIsItsRowsCopiedAndMayBeEmpty()
    {
        var mine = new List<AdvancedRow> { new("Vertical Sync (0x00A879CF)", "0x60925292", "user_set") };

        AdvancedValue v = AdvancedValue.OfRows(mine);
        mine.Clear();

        v.Rows.Should().Equal(new AdvancedRow("Vertical Sync (0x00A879CF)", "0x60925292", "user_set"));
        AdvancedValue.OfRows([]).Rows.Should().BeEmpty("a profile that sets nothing is an answer");
        FluentActions.Invoking(static () => AdvancedValue.OfRows(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AFactAndAReadingCarryWhatTheyWereGiven()
    {
        var fact = new AdvancedFact("nvapi.display.luminance_max", "2", AdvancedValue.Of(417));
        var reading = new AdvancedReading("adv.lhm.board.voltage.lpc_0_7", -11.97);

        fact.Id.Should().Be("nvapi.display.luminance_max");
        fact.Instance.Should().Be("2");
        fact.Value.Number.Should().Be(417);
        reading.Series.Should().Be("adv.lhm.board.voltage.lpc_0_7");
        reading.Value.Should().Be(-11.97, "a voltage rail is negative, and that is a reading (D102)");
    }
}

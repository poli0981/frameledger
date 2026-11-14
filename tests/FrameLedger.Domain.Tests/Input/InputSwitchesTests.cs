// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Input;

namespace FrameLedger.Domain.Tests.Input;

/// <summary>
/// How a tick is named by what it was played with, how changes are counted, and when two entries of a session's
/// controller list are one pad (<c>03_METRICS</c> §Input).
/// </summary>
public sealed class InputSwitchesTests
{
    [Fact]
    public void AChangeIsCountedBetweenTwoDifferentValuesAndNothingInBetweenEndsARun()
    {
        InputClass?[] ticks =
        [
            null,
            InputClass.Controller,
            InputClass.Controller,
            null, // the controller put down for a moment
            InputClass.Controller,
            InputClass.KeyboardMouse,
            null,
            null,
            InputClass.Controller,
        ];

        InputSwitches.Count(ticks).Should().Be(2);
    }

    [Fact]
    public void NoTicksAndOneValueAreNoChange()
    {
        InputSwitches.Count(Array.Empty<InputClass?>()).Should().Be(0);
        InputSwitches.Count(new InputClass?[] { null, null }).Should().Be(0);
        InputSwitches.Count(new ControllerFamily?[] { ControllerFamily.Xbox, ControllerFamily.Xbox }).Should().Be(0);
        InputSwitches.Count(new ControllerFamily?[] { ControllerFamily.Xbox, ControllerFamily.PlayStation, ControllerFamily.Xbox }).Should().Be(2);
    }

    [Theory]
    [InlineData(900, 0, 0, InputClass.Controller)]
    [InlineData(0, 400, 0, InputClass.KeyboardMouse)]
    public void ATickPlayedWithOneThingIsThatThing(long controller, long keyboardMouse, long both, InputClass expected) =>
        InputSwitches.Exclusive(controller, keyboardMouse, both).Should().Be(expected);

    [Theory]
    [InlineData(0, 0, 0)] // neither
    [InlineData(500, 500, 0)] // one then the other within the tick
    [InlineData(900, 0, 100)] // both at once for a moment
    public void ATickPlayedWithBothOrWithNeitherIsNoOneThing(long controller, long keyboardMouse, long both) =>
        InputSwitches.Exclusive(controller, keyboardMouse, both).Should().BeNull();

    [Theory]
    [InlineData(600, 300, 0, 100, InputClass.Controller)]
    [InlineData(100, 700, 0, 200, InputClass.KeyboardMouse)]
    [InlineData(100, 100, 500, 300, InputClass.Both)]
    [InlineData(100, 100, 100, 700, InputClass.None)]
    [InlineData(0, 0, 0, 1000, InputClass.None)]
    [InlineData(500, 500, 0, 0, InputClass.KeyboardMouse)] // a tie goes to the one named first: none, keyboard, controller, both
    public void TheDominantClassIsTheOneWithTheMostTime(long controller, long keyboardMouse, long both, long idle, InputClass expected) =>
        InputSwitches.Dominant(controller, keyboardMouse, both, idle).Should().Be(expected);

    [Fact]
    public void ATickWithTheGameNotInFrontHasNoDominantClass() =>
        InputSwitches.Dominant(0, 0, 0, 0).Should().BeNull();

    [Fact]
    public void TwoPadsThatMoveTogetherAreOnePad()
    {
        int[] held = [1000, 1000, 0, 1000, 900, 1000, 1000, 1000, 1000, 1000, 1000, 1000];
        int[] presented = [1000, 900, 0, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000];

        VirtualPad.Coincidence(held, presented).Should().BeGreaterThan(0.95);
        VirtualPad.IsSamePad(held, presented).Should().BeTrue();
    }

    [Fact]
    public void TwoPadsUsedAtDifferentMomentsAreTwoPads()
    {
        int[] first = [1000, 1000, 1000, 1000, 1000, 1000, 0, 0, 0, 0, 0, 0];
        int[] second = [0, 0, 0, 0, 0, 0, 1000, 1000, 1000, 1000, 1000, 1000];

        VirtualPad.Coincidence(first, second).Should().Be(0);
        VirtualPad.IsSamePad(first, second).Should().BeFalse();
    }

    [Fact]
    public void APadThatSharesOnlyPartOfTheBusierOnesTimeIsNotIt()
    {
        // Two players: the second joins for a third of the time the first plays.
        int[] first = [1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000];
        int[] second = [1000, 1000, 1000, 1000, 0, 0, 0, 0, 0, 0, 0, 0];

        VirtualPad.Coincidence(first, second).Should().BeApproximately(1 / 3d, 1e-9);
        VirtualPad.IsSamePad(first, second).Should().BeFalse();
    }

    [Fact]
    public void AFewSecondsTogetherProveNothing()
    {
        int[] a = [1000, 1000, 1000];
        int[] b = [1000, 1000, 1000];

        VirtualPad.Coincidence(a, b).Should().Be(1);
        VirtualPad.IsSamePad(a, b).Should().BeFalse("under ten seconds in use");
        VirtualPad.Coincidence([], []).Should().Be(0);
        VirtualPad.Coincidence([0, 0], [0, 0]).Should().Be(0);
    }
}

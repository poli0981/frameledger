// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Recording;

namespace FrameLedger.Application.Tests.Recording;

/// <summary>beta.14: a crash reporter the game started is its crash only when it started after the game's first seconds and the game left within a minute of it.</summary>
public sealed class CrashReporterRuleTests
{
    private static readonly DateTimeOffset _gameStart = new(2026, 10, 5, 20, 0, 0, TimeSpan.Zero);

    private static CrashReporterSighting Sighting(TimeSpan afterGameStart) =>
        new("CrashReportClient.exe", 100, @"C:\Games\Game.exe", _gameStart, _gameStart + afterGameStart);

    private static CrashQuery Query(bool left) => new(@"C:\Games\Game.exe", 100, _gameStart, _gameStart.AddHours(2), left);

    [Fact]
    public void AReporterAndThenTheGamesExitIsACrash() =>
        CrashReporterRule.Counts(Sighting(TimeSpan.FromMinutes(20)), Query(left: true), _gameStart.AddMinutes(20).AddSeconds(8)).Should().BeTrue();

    [Fact]
    public void AReporterThatStartedWithTheGameIsAMonitorNotACrash() =>
        CrashReporterRule.Counts(Sighting(TimeSpan.FromSeconds(3)), Query(left: true), _gameStart.AddSeconds(30)).Should().BeFalse();

    [Fact]
    public void AGameThatKeptRunningReportedSomethingItSurvived() =>
        CrashReporterRule.Counts(Sighting(TimeSpan.FromMinutes(20)), Query(left: true), _gameStart.AddMinutes(45)).Should().BeFalse();

    [Fact]
    public void ASessionTheUserStoppedWhileTheGameRanIsNoCrash() =>
        CrashReporterRule.Counts(Sighting(TimeSpan.FromMinutes(20)), Query(left: false), _gameStart.AddMinutes(20).AddSeconds(8)).Should().BeFalse();
}

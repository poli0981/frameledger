// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Recording;

namespace FrameLedger.Application.Tests.Recording;

/// <summary>
/// beta.14: an Application-log record is a crash of THIS game, or it is not. Until then any record 1000 or 1001 whose text
/// contained the executable's name counted — and on the owner's machine (2026-10-04) Windows' memory-leak report for
/// The Witcher 3, a WER 1001 named <c>RADAR_PRE_LEAK_64</c>, made a session that ended with exit code 0 read "crashed".
/// </summary>
public sealed class CrashEventMatcherTests
{
    private const string _witcher = @"D:\SteamLibrary\steamapps\common\The Witcher 3\bin\x64_dx12\witcher3.exe";
    private const string _game = @"C:\Games\Title\Game.exe";

    private static CrashQuery Query(string exe = _witcher, int pid = 0) =>
        new(exe, pid, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1), TargetLeft: true);

    /// <summary>A WER 1001 as the log types it: [0] bucket, [1] type, [2] event name, [3] response, [4] cab id, [5] P1 …</summary>
    private static CrashLogRecord Wer(string eventName, string p1) =>
        new(1001, CrashEventMatcher.WerProvider, ["2300524621932593539", "5", eventName, "Not available", "0", p1, "", "", "", "", "", ""]);

    /// <summary>An Application Error 1000: [0] AppName … [8] ProcessId, [9] ProcessCreationTime, [10] AppPath.</summary>
    private static CrashLogRecord ApplicationError(string appName, object? pid, string? appPath) =>
        new(1000, CrashEventMatcher.ApplicationErrorProvider,
            [appName, "1.0.0.0", "00000000", "ntdll.dll", "10.0", "00000000", "c0000005", "000000000002f1b0", pid, 0x01DB3F2A0C1E8000UL, appPath, @"C:\Windows\SYSTEM32\ntdll.dll", "id"]);

    [Fact]
    public void TheWitcherMemoryLeakReportIsNotACrash() =>
        CrashEventMatcher.Matches(Wer("RADAR_PRE_LEAK_64", "witcher3.exe"), Query()).Should().BeFalse("RADAR is Windows' leak detector reporting high memory use");

    [Fact]
    public void AHangReportIsNotACrash() =>
        CrashEventMatcher.Matches(Wer("AppHangB1", "witcher3.exe"), Query()).Should().BeFalse("a game the user closed while it hung did not crash");

    [Theory]
    [InlineData("APPCRASH")]
    [InlineData("BEX64")]
    [InlineData("CLR20r3")]
    public void AWerCrashReportOfThisGameIsACrash(string eventName) =>
        CrashEventMatcher.Matches(Wer(eventName, "witcher3.exe"), Query()).Should().BeTrue();

    [Fact]
    public void AnotherProgramWhoseNameContainsTheGamesIsNotThisGame() =>
        CrashEventMatcher.Matches(Wer("APPCRASH", "MyGame.exe"), Query(_game)).Should().BeFalse("the old substring rule found Game.exe inside MyGame.exe");

    [Fact]
    public void AnApplicationErrorOfThisProcessIsACrashAndOfAnotherProcessIsNot()
    {
        CrashEventMatcher.Matches(ApplicationError("Game.exe", 0x1234u, _game), Query(_game, 0x1234)).Should().BeTrue();
        CrashEventMatcher.Matches(ApplicationError("Game.exe", "0x1234", _game), Query(_game, 0x1234)).Should().BeTrue("the pid may come as hex text");
        CrashEventMatcher.Matches(ApplicationError("Game.exe", 0x9999u, _game), Query(_game, 0x1234)).Should().BeFalse("another run of the game, or a same-image child");
        CrashEventMatcher.Matches(ApplicationError("Game.exe", 0x1234u, @"D:\Other\Game.exe"), Query(_game, 0x1234)).Should().BeFalse("another executable of the same name");
        CrashEventMatcher.Matches(ApplicationError("Game.exe", 0x1234u, null), Query(_game)).Should().BeTrue("a session that held no pid matches on the name and the path the record gives");
    }

    [Fact]
    public void ARecordFromAnotherProviderIsNotACrash() =>
        CrashEventMatcher.Matches(new CrashLogRecord(1000, "Application Hang", ["Game.exe"]), Query(_game)).Should().BeFalse();
}

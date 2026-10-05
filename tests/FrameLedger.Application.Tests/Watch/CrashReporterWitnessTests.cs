// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Watch;

namespace FrameLedger.Application.Tests.Watch;

/// <summary>
/// beta.14: the engine crash reporters a game starts, seen from the watcher's own snapshots. Unreal handles its crash
/// itself, starts <c>CrashReportClient.exe</c> and exits with code 3 — no exception code, no Application-log record — so
/// the reporter is the one witness. Seen as the game's child by pid AND start, never by a reused pid; an always-on handler
/// (Unity's, crashpad) never.
/// </summary>
public sealed class CrashReporterWitnessTests
{
    private const string _game = @"C:\Games\SILENT HILL 2\SHProto\Binaries\Win64\SHProto-Win64-Shipping.exe";
    private static readonly DateTimeOffset _t0 = new(2026, 10, 5, 20, 0, 0, TimeSpan.Zero);

    private static ProcessSnapshot Game(DateTimeOffset started) => new(100, 4, "SHProto-Win64-Shipping.exe", _game, started);

    private static ProcessSnapshot Reporter(string image, int parent, DateTimeOffset started) =>
        new(200, parent, image, @"C:\Games\SILENT HILL 2\Engine\Binaries\Win64\" + image, started);

    private static CrashQuery Query(bool left = false) => new(_game, 100, _t0, _t0.AddHours(1), left);

    [Fact]
    public async Task AReporterTheGameStartedIsItsChild()
    {
        var witness = new CrashReporterWitness();
        witness.Observe([Game(_t0)], _t0.AddSeconds(1));
        witness.Observe([Game(_t0), Reporter("CrashReportClient.exe", 100, _t0.AddMinutes(20))], _t0.AddMinutes(20).AddSeconds(1));

        CrashReporterSighting? s = await witness.FindAsync(Query(), TestContext.Current.CancellationToken);

        s.Should().NotBeNull();
        s!.Image.Should().Be("CrashReportClient.exe");
        (s.ParentImagePath, s.ParentStartedAt).Should().Be((_game, (DateTimeOffset?)_t0));
    }

    [Fact]
    public async Task TheGameGoneBeforeTheReporterWasSeenIsRememberedFromTheSnapshotBefore()
    {
        var witness = new CrashReporterWitness();
        witness.Observe([Game(_t0)], _t0.AddMinutes(20));
        witness.Observe([Reporter("CrashReportClient.exe", 100, _t0.AddMinutes(20))], _t0.AddMinutes(20).AddSeconds(1));

        (await witness.FindAsync(Query(), TestContext.Current.CancellationToken)).Should().NotBeNull("the dying game started it; the snapshot before saw the game");
    }

    [Fact]
    public async Task APidReusedByAProcessThatStartedAfterTheReporterIsNotItsParent()
    {
        var witness = new CrashReporterWitness();
        DateTimeOffset reporterStart = _t0.AddMinutes(20);
        witness.Observe([new ProcessSnapshot(100, 4, "notepad.exe", @"C:\Windows\notepad.exe", reporterStart.AddSeconds(5)),
            Reporter("CrashReportClient.exe", 100, reporterStart)], reporterStart.AddSeconds(6));

        (await witness.FindAsync(Query(), TestContext.Current.CancellationToken)).Should().BeNull();
    }

    [Theory]
    [InlineData("UnityCrashHandler64.exe")]
    [InlineData("crashpad_handler.exe")]
    public async Task AnAlwaysOnHandlerIsNeverASighting(string image)
    {
        var witness = new CrashReporterWitness();
        witness.Observe([Game(_t0), Reporter(image, 100, _t0.AddSeconds(1))], _t0.AddSeconds(2));

        (await witness.FindAsync(Query(), TestContext.Current.CancellationToken)).Should().BeNull("it starts with the game; its presence says nothing");
    }

    [Fact]
    public async Task WithNoPollAtAllTheAnswerIsImmediate()
    {
        var witness = new CrashReporterWitness();
        var started = System.Diagnostics.Stopwatch.StartNew();

        (await witness.FindAsync(Query(left: true), TestContext.Current.CancellationToken)).Should().BeNull();
        started.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1), "the console path never polls, so it never waits");
    }

    [Fact]
    public async Task AGameThatJustLeftGetsTwoMorePollsForItsReporter()
    {
        var witness = new CrashReporterWitness();
        witness.Observe([Game(_t0)], _t0.AddMinutes(20));
        Task<CrashReporterSighting?> asked = witness.FindAsync(Query(left: true), TestContext.Current.CancellationToken);

        await Task.Delay(150, TestContext.Current.CancellationToken);
        witness.Observe([Reporter("CrashReportClient.exe", 100, _t0.AddMinutes(20))], _t0.AddMinutes(20).AddSeconds(1));
        await Task.Delay(150, TestContext.Current.CancellationToken);
        witness.Observe([], _t0.AddMinutes(20).AddSeconds(2));

        (await asked).Should().NotBeNull("the reporter a dying game starts arrives in the next snapshot");
    }
}

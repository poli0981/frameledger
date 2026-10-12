// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Tests.Ipc;
using FrameLedger.Application.Watch;
using FrameLedger.Domain.Consent;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.Application.Tests.Watch;

/// <summary>
/// beta.19, owner decision D55: an entry whose game is no longer installed and that has no session goes — after ten minutes,
/// with the switch on, never while a session of it runs or waits, never one carrying an anti-cheat block or exception, and
/// never for a drive that is not there, a different disk under the same letter, or a folder Windows will not list.
/// </summary>
public sealed class UninstalledEntryPruneTests
{
    private const string _path = @"D:\another\it\uninstalled\Game.exe";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;

        public void Advance(TimeSpan by) => Now += by;
    }

    private sealed class FakePresence : IExecutablePresence
    {
        public ExecutablePresence Answer { get; set; } = ExecutablePresence.FileGone;

        public ExecutablePresence Probe(string exePath) => Answer;
    }

    private sealed class FakePrune : IGamePrune
    {
        /// <summary>What the stored conditions say; true unless the test says the row has sessions.</summary>
        public bool Deletes { get; set; } = true;

        public List<(long Id, string Path)> Asked { get; } = [];

        public ValueTask<bool> RemoveUninstalledAsync(long gameId, string exePath, CancellationToken ct = default)
        {
            Asked.Add((gameId, exePath));
            return ValueTask.FromResult(Deletes);
        }
    }

    private sealed class Rig
    {
        public ManualClock Clock { get; } = new();

        public FakePresence Presence { get; } = new();

        public FakePrune Prune { get; } = new();

        public RecordingPublisher Pipe { get; } = new();

        public List<string> Log { get; } = [];

        public bool On { get; set; } = true;

        public bool Busy { get; set; }

        public UninstalledEntryPrune Build() =>
            new(Prune, Presence, _ => ValueTask.FromResult(On), _ => Busy, Log.Add, Pipe, Clock);
    }

    private static GameRow Row(string path = _path, long id = 7) => new()
    {
        Id = id,
        Name = "Uninstalled",
        Fingerprint = new ExecutableFingerprint { ExePath = path, SizeBytes = 1, MtimeUnixMs = 1 },
        HookEnabled = false,
        HookCrashCount = 0,
        AddedAt = DateTimeOffset.UnixEpoch,
        UpdatedAt = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public async Task AGoneFileIsRemovedOnlyOnceItHasBeenGoneTenMinutesAndTheAppIsTold()
    {
        var rig = new Rig();
        UninstalledEntryPrune prune = rig.Build();

        (await prune.ConsiderAsync(Row(), Ct)).Should().BeFalse("the first sweep that sees it gone starts the count");
        rig.Clock.Advance(TimeSpan.FromMinutes(9));
        (await prune.ConsiderAsync(Row(), Ct)).Should().BeFalse("nine minutes is a store replacing the file, perhaps");
        rig.Prune.Asked.Should().BeEmpty();

        rig.Clock.Advance(TimeSpan.FromMinutes(1));
        (await prune.ConsiderAsync(Row(), Ct)).Should().BeTrue();

        rig.Prune.Asked.Should().Equal([(7L, _path)]);
        rig.Log.Should().ContainSingle().Which.Should().Contain("removed 'Uninstalled'").And.Contain(_path).And.Contain("no session");
        rig.Pipe.Of<GameRemovedEvent>().Should().ContainSingle().Which.Should().Be(new GameRemovedEvent(7, "Uninstalled", _path, GameRemovedEvent.Uninstalled));
    }

    [Theory]
    [InlineData(ExecutablePresence.DriveAbsent)]
    [InlineData(ExecutablePresence.OnlyTheDriveRemains)]
    [InlineData(ExecutablePresence.Unknown)]
    [InlineData(ExecutablePresence.Present)]
    public async Task AnythingButAFileGoneFromAFolderThatIsStillThereRemovesNothingAndStartsTheCountAgain(ExecutablePresence presence)
    {
        var rig = new Rig();
        UninstalledEntryPrune prune = rig.Build();
        _ = await prune.ConsiderAsync(Row(), Ct);
        rig.Clock.Advance(TimeSpan.FromMinutes(30));

        rig.Presence.Answer = presence;
        (await prune.ConsiderAsync(Row(), Ct)).Should().BeFalse();

        rig.Presence.Answer = ExecutablePresence.FileGone;
        rig.Clock.Advance(TimeSpan.FromMinutes(1));
        (await prune.ConsiderAsync(Row(), Ct)).Should().BeFalse("the count started again when the answer changed");
        rig.Prune.Asked.Should().BeEmpty();
    }

    [Fact]
    public async Task AFileThatReadsAgainOrAnEntryReRepointedStartsTheCountAgain()
    {
        var rig = new Rig();
        UninstalledEntryPrune prune = rig.Build();
        _ = await prune.ConsiderAsync(Row(), Ct);
        rig.Clock.Advance(TimeSpan.FromMinutes(20));
        prune.Present(7);
        (await prune.ConsiderAsync(Row(), Ct)).Should().BeFalse("it read in between");

        rig.Clock.Advance(TimeSpan.FromMinutes(20));
        (await prune.ConsiderAsync(Row(@"D:\another\it\uninstalled\Other.exe"), Ct)).Should().BeFalse("Change executable re-pointed it");
        rig.Prune.Asked.Should().BeEmpty();
    }

    [Fact]
    public async Task TheSwitchOffABusyEntryAndAnAntiCheatEntryAreKept()
    {
        var rig = new Rig { On = false };
        UninstalledEntryPrune prune = rig.Build();
        _ = await prune.ConsiderAsync(Row(), Ct);
        rig.Clock.Advance(TimeSpan.FromMinutes(15));
        (await prune.ConsiderAsync(Row(), Ct)).Should().BeFalse("Settings ▸ Data turned it off");

        rig.On = true;
        rig.Busy = true;
        (await prune.ConsiderAsync(Row(), Ct)).Should().BeFalse("a session of it is recorded, or waits to be recovered");

        rig.Busy = false;
        (await prune.ConsiderAsync(Row() with { HookBlockedReason = "BlockedModule EasyAntiCheat" }, Ct)).Should().BeFalse("rule 2: nothing clears a block");
        (await prune.ConsiderAsync(Row() with { HookPrescanState = "blocked" }, Ct)).Should().BeFalse();
        (await prune.ConsiderAsync(Row() with { AcException = AntiCheatExceptionState.None with { GrantedAt = DateTimeOffset.UnixEpoch, Family = "NetEase Yidun" } }, Ct))
            .Should().BeFalse("an exception's grant stays with its entry");
        (await prune.ConsiderAsync(Row() with { AcException = AntiCheatExceptionState.None with { TrialFailedAt = DateTimeOffset.UnixEpoch } }, Ct))
            .Should().BeFalse("D38: a failed trial ends the exception for good");
        rig.Prune.Asked.Should().BeEmpty();

        (await prune.ConsiderAsync(Row(), Ct)).Should().BeTrue("the switch on, nothing running, no anti-cheat: it goes");
    }

    [Fact]
    public async Task AnEntryTheDeleteKeptBecauseItHasSessionsIsNotAskedAgain()
    {
        var rig = new Rig();
        rig.Prune.Deletes = false;
        UninstalledEntryPrune prune = rig.Build();
        _ = await prune.ConsiderAsync(Row(), Ct);
        rig.Clock.Advance(TimeSpan.FromMinutes(10));

        (await prune.ConsiderAsync(Row(), Ct)).Should().BeFalse();
        (await prune.ConsiderAsync(Row(), Ct)).Should().BeFalse();

        rig.Prune.Asked.Should().ContainSingle("the stored conditions said no once; the answer will not change every 15 seconds");
        rig.Log.Should().BeEmpty();
        rig.Pipe.Published.Should().BeEmpty();

        // An hour on, its sessions may have been deleted (FR-1.7): it is asked again.
        rig.Prune.Deletes = true;
        rig.Clock.Advance(UninstalledEntryPrune.RetryAfter);
        (await prune.ConsiderAsync(Row(), Ct)).Should().BeTrue();
        rig.Prune.Asked.Should().HaveCount(2);
    }
}

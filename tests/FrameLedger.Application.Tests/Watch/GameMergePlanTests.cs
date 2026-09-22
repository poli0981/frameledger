using System.Globalization;
using FluentAssertions;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Consent;

namespace FrameLedger.Application.Tests.Watch;

/// <summary>Which of two twin entries stays (2026-09-23, D26): the one made first, a tie keeping the lower id.</summary>
public sealed class GameMergePlanTests
{
    private static GameRow Row(long id, string path, string added) => new()
    {
        Id = id,
        Name = "T" + id.ToString(CultureInfo.InvariantCulture),
        Fingerprint = new ExecutableFingerprint { ExePath = path, SizeBytes = 1, MtimeUnixMs = 1 },
        HookEnabled = false,
        HookCrashCount = 0,
        AddedAt = DateTimeOffset.Parse(added, CultureInfo.InvariantCulture),
        UpdatedAt = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public void TheEntryMadeFirstStaysAndATieKeepsTheLowerId()
    {
        GameRow stale = Row(7, @"D:\G\g.exe", "2026-09-01T00:00:00Z");
        GameRow owner = Row(9, @"H:\G\g.exe", "2026-09-22T09:39:00Z");

        GameMergePlan first = GameMergePlan.For(stale, owner, owner.Fingerprint);
        first.SurvivorId.Should().Be(7);
        first.DroppedId.Should().Be(9);
        first.SurvivorPath.Should().Be(@"D:\G\g.exe");
        first.SurvivorMoves.Should().BeTrue("the stale entry takes the file's path");

        GameMergePlan later = GameMergePlan.For(stale with { AddedAt = owner.AddedAt.AddDays(1) }, owner, owner.Fingerprint);
        later.SurvivorId.Should().Be(9);
        later.SurvivorMoves.Should().BeFalse("the entry already at the file stays where it is");

        GameMergePlan.For(stale with { Id = 12, AddedAt = owner.AddedAt }, owner, owner.Fingerprint).SurvivorId.Should().Be(9, "a tie keeps the lower id");
    }

    [Fact]
    public void AnEntryIsNotItsOwnTwinAndTheOwnerIsTheEntryAtTheTarget()
    {
        GameRow a = Row(1, @"D:\G\g.exe", "2026-09-01T00:00:00Z");
        GameRow b = Row(2, @"H:\G\g.exe", "2026-09-02T00:00:00Z");

        ((Action)(() => GameMergePlan.For(a, a, a.Fingerprint))).Should().Throw<ArgumentException>();
        ((Action)(() => GameMergePlan.For(a, b, a.Fingerprint))).Should().Throw<ArgumentException>("the owner must be the entry at the file");
    }
}

using FluentAssertions;
using FrameLedger.Application.Persistence;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.Infrastructure.Tests.Persistence;

/// <summary><c>hardware_snapshots</c>: deduplicated by the normalised hash, the display columns included (they were "null until P3").</summary>
public sealed class SqliteHardwareSnapshotRepositoryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheSameHardwareIsOneRowAndADifferentDisplayModeIsAnother()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteHardwareSnapshotRepository(f.Db);
        var a = new HardwareSnapshot { CpuName = "i7-14700KF", GpuName = "RTX 5080", GpuDriver = "581.29", RamGb = 31.8, OsBuild = "10.0.26100", DisplayRes = "2560x1440", DisplayHz = 144 };

        long id = await repo.EnsureAsync(a, DateTimeOffset.UnixEpoch, Ct);
        (await repo.EnsureAsync(a with { CpuName = "  i7-14700kf " }, DateTimeOffset.UnixEpoch.AddDays(1), Ct)).Should().Be(id, "the hash normalises case and whitespace");
        (await repo.EnsureAsync(a with { DisplayHz = 60 }, DateTimeOffset.UnixEpoch, Ct)).Should().NotBe(id, "a monitor change is a hardware change (FR-6.3)");

        HardwareSnapshot back = (await repo.FindAsync(id, Ct))!;
        back.Should().BeEquivalentTo(a);
        back.Hash.Should().Be(a.Hash);
        (await repo.FindAsync(id + 99, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task AnUnknownFieldIsNullNotEmpty()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteHardwareSnapshotRepository(f.Db);
        long id = await repo.EnsureAsync(new HardwareSnapshot { GpuName = "G" }, DateTimeOffset.UnixEpoch, Ct);
        HardwareSnapshot back = (await repo.FindAsync(id, Ct))!;
        back.CpuName.Should().BeNull();
        back.DisplayRes.Should().BeNull();
        back.DisplayHz.Should().BeNull();
    }
}

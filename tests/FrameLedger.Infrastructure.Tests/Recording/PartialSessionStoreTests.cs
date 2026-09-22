using FluentAssertions;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Sessions;
using FrameLedger.Infrastructure.Recording;

namespace FrameLedger.Infrastructure.Tests.Recording;

public sealed class PartialSessionStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-pstore-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static PartialHeader Header(Guid guid, long gameId = 1) => new()
    {
        SessionGuid = guid,
        StartedAt = DateTimeOffset.UnixEpoch,
        QpcEpoch = 1,
        QpcFrequency = 10_000_000,
        GameId = gameId,
        SnapshotId = 1,
        ExePath = @"C:\g\game.exe",
        Tier = CaptureTier.NotHooked,
        Mode = CaptureMode.Attach,
    };

    /// <summary>
    /// The entries pending files name (2026-09-23): what a merge of two entries waits for. Read beside a running session's
    /// writer, which shares its file for reading only — a plain read would fail on it — and a damaged file names nothing.
    /// </summary>
    [Fact]
    public void PendingGameIdsAreReadBesideARunningSessionsWriter()
    {
        var store = new PartialSessionStore(_dir);
        store.PendingGameIds().Should().BeEmpty("no directory yet is no sessions");

        using IPartialSessionWriter running = store.Create(Header(Guid.NewGuid(), gameId: 7));
        store.Create(Header(Guid.NewGuid(), gameId: 9)).Dispose();
        File.WriteAllBytes(Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".partial"), [1, 2, 3]);

        store.PendingGameIds().Should().BeEquivalentTo([7L, 9L]);
    }

    [Fact]
    public void CreatesUnderTheDirectoryListsOldestFirstReadsBackAndDeletes()
    {
        var store = new PartialSessionStore(Path.Combine(_dir, "tmp"));
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        store.ListPending().Should().BeEmpty("no directory yet is no sessions, not an error");
        store.Create(Header(a)).Dispose();
        Thread.Sleep(20);
        store.Create(Header(b)).Dispose();
        File.WriteAllText(Path.Combine(store.Directory, "not-a-guid.partial"), "x");

        store.ListPending().Should().Equal(a, b);
        store.Read(a)!.Header.SessionGuid.Should().Be(a);
        store.Read(Guid.NewGuid()).Should().BeNull();

        store.Delete(a);
        store.Delete(a);
        store.ListPending().Should().Equal(b);
    }

    /// <summary>A file recovery could not finalize is kept for a bug report and never listed again (2026-09-23).</summary>
    [Fact]
    public void AQuarantinedFileIsKeptBesideTheOthersAndNeverListedAgain()
    {
        var store = new PartialSessionStore(Path.Combine(_dir, "tmp"));
        var bad = Guid.NewGuid();
        var good = Guid.NewGuid();
        store.Create(Header(bad)).Dispose();
        store.Create(Header(good)).Dispose();

        store.Quarantine(bad);
        store.Quarantine(bad);
        store.Quarantine(Guid.NewGuid());

        store.ListPending().Should().Equal(good);
        store.Read(bad).Should().BeNull("it is no longer a pending file");
        File.Exists(Path.Combine(store.Directory, bad.ToString("N") + ".partial.failed")).Should().BeTrue("kept, for a bug report");
    }
}

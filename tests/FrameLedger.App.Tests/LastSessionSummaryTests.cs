using System.Text.Json;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.App.Tests;

/// <summary>
/// The last session's summary (<c>10_LOGGING</c> §Bug report flow step 2): the newest session, as File ▸ Export's
/// <c>session.json</c> without the user's notes and tags; nothing when the ledger has no session.
/// </summary>
public sealed class LastSessionSummaryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheNewestSessionIsOfferedAndWrittenWithoutNotesOrTags()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var summary = new LastSessionSummary(s.Sessions, s.Games, new SqliteHardwareSnapshotRepository(s.Db));
        (await summary.FindAsync(Ct)).Should().BeNull("an empty ledger offers nothing");

        GameRow older = await s.GameAsync("Older");
        GameRow newer = await s.GameAsync("Newer");
        await s.SessionAsync(older.Id, new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero));
        long id = await s.SessionAsync(newer.Id, new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero));
        await s.Annotations.UpsertAsync(new SessionAnnotation { SessionId = id, Notes = "private note", Tags = ["mine"] }, Ct);

        LastSessionInfo? info = await summary.FindAsync(Ct);
        byte[]? json = await summary.JsonAsync(id, Ct);

        info.Should().Be(new LastSessionInfo(id, "Newer", new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero)));
        json.Should().NotBeNull();
        using JsonDocument doc = JsonDocument.Parse(json!);
        doc.RootElement.GetProperty("game").GetString().Should().Be("Newer");
        System.Text.Encoding.UTF8.GetString(json!).Should().NotContain("private note").And.NotContain("mine");
        (await summary.JsonAsync(id + 1000, Ct)).Should().BeNull("a session that is gone writes nothing");
    }
}

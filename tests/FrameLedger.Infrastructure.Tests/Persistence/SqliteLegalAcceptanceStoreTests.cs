using FluentAssertions;
using FrameLedger.Application.Persistence;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.Infrastructure.Tests.Persistence;

/// <summary><c>legal_acceptance</c>: one row per document, re-recorded on a version increment (FR-11).</summary>
public sealed class SqliteLegalAcceptanceStoreTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ADocumentIsRecordedOncePerVersionAndListedInOrder()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteLegalAcceptanceStore(f.Db);
        (await store.FindAsync("EULA", Ct)).Should().BeNull();
        (await store.ListAsync(Ct)).Should().BeEmpty();

        var first = new LegalAcceptance("EULA", "2026-09-13", DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000));
        await store.RecordAsync(first, Ct);
        await store.RecordAsync(new LegalAcceptance("DISCLAIMER", "2026-09-13", first.AcceptedAt), Ct);
        (await store.FindAsync("EULA", Ct)).Should().Be(first);

        var newer = first with { Version = "2026-10-01", AcceptedAt = first.AcceptedAt.AddDays(20) };
        await store.RecordAsync(newer, Ct);
        (await store.FindAsync("EULA", Ct)).Should().Be(newer, "a version increment replaces the row");
        (await store.ListAsync(Ct)).Select(static a => a.Document).Should().Equal("DISCLAIMER", "EULA");

        Func<Task> blank = async () => await store.RecordAsync(new LegalAcceptance("EULA", " ", first.AcceptedAt), Ct).ConfigureAwait(true);
        await blank.Should().ThrowAsync<ArgumentException>();
    }
}

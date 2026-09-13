using FluentAssertions;
using FrameLedger.Application.Settings;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.Infrastructure.Tests.Persistence;

/// <summary>The raw <c>settings</c> table and the registry over it, against SQLite.</summary>
public sealed class SqliteSettingsStoreTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AbsentIsNullAndAWriteIsAnUpsert()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteSettingsStore(f.Db);

        (await store.GetAsync("ui.theme", Ct)).Should().BeNull();
        await store.SetAsync("ui.theme", "dark", Ct);
        await store.SetAsync("ui.theme", "light", Ct);
        (await store.GetAsync("ui.theme", Ct)).Should().Be("light");

        Func<Task> blankKey = async () => await store.SetAsync(" ", "x", Ct).ConfigureAwait(true);
        await blankKey.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TheRegistryReadsTolerantlyAndWritesExactlyThroughTheTable()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteSettingsStore(f.Db);
        var settings = new RegisteredSettings(store);

        (await settings.GetIntegerAsync(SettingsRegistry.RetentionRawSessionsPerGame, Ct)).Should().Be(20);
        await store.SetAsync(SettingsRegistry.RetentionRawSessionsPerGame.Key, "lots", Ct);
        (await settings.GetIntegerAsync(SettingsRegistry.RetentionRawSessionsPerGame, Ct)).Should().Be(20, "a hand-edited row reads as the default");

        await settings.SetAsync(SettingsRegistry.RetentionRawSessionsPerGame, 5, Ct);
        (await store.GetAsync(SettingsRegistry.RetentionRawSessionsPerGame.Key, Ct)).Should().Be("5");

        // Another process's view (the Agent's connection) sees the UI's write on its next read: no cache anywhere.
        await using var other = await f.OpenAnotherAsync(Ct);
        (await new RegisteredSettings(new SqliteSettingsStore(other)).GetIntegerAsync(SettingsRegistry.RetentionRawSessionsPerGame, Ct)).Should().Be(5);
    }
}

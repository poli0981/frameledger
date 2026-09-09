using FluentAssertions;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Infrastructure.Settings;

namespace FrameLedger.Infrastructure.Tests.Persistence;

/// <summary>FR-2.4 as one settings row: absent is off, exactly "1" is on, and every read is fresh.</summary>
public sealed class SettingsKillSwitchTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AbsentIsOffOneIsOnAndTheReadIsNeverCached()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var settings = new SqliteSettingsStore(f.Db);
        var sw = new SettingsKillSwitch(settings);

        (await sw.IsEngagedAsync(Ct)).Should().BeFalse("no row means nobody turned it on");

        await sw.SetAsync(true, Ct);
        (await sw.IsEngagedAsync(Ct)).Should().BeTrue();
        (await settings.GetAsync(SettingsKillSwitch.Key, Ct)).Should().Be("1", "06_DATA_MODEL names the key and the value");

        // The UI (P3) writes the same row through the same store; the switch sees it on the next ask.
        await settings.SetAsync(SettingsKillSwitch.Key, "0", Ct);
        (await sw.IsEngagedAsync(Ct)).Should().BeFalse();

        await settings.SetAsync(SettingsKillSwitch.Key, "yes", Ct);
        (await sw.IsEngagedAsync(Ct)).Should().BeFalse("only the one value means engaged; anything else is not a stop nobody asked for");
    }
}

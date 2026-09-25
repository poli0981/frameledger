using FluentAssertions;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Recording;

namespace FrameLedger.Application.Tests.Recording;

/// <summary>
/// <c>sessions.driver_profile</c> (schema 0012): a reading round-trips with its ids as hex text; a reading never asked
/// stores nothing; a column this build cannot read is null, never an exception.
/// </summary>
public sealed class DriverProfileRecordTests
{
    [Fact]
    public void AReadingRoundTripsWithItsIdsAsHex()
    {
        var reading = new DriverProfileReading
        {
            Outcome = DriverProfileOutcome.Application,
            ProfileName = "Clair Obscur: Expedition 33",
            NvapiStatus = 0,
            Settings =
            [
                new DriverSettingReading(NvidiaDriverSettings.DlssSrOverride, 1, DriverSettingLocation.Profile, Predefined: false),
                new DriverSettingReading(NvidiaDriverSettings.SmoothMotion, null, DriverSettingLocation.NotSet, Predefined: false),
            ],
        };

        string? json = DriverProfileRecord.Serialize(reading);

        json.Should().Contain("\"Id\":\"0x10E41E01\"").And.Contain("\"Outcome\":\"Application\"");
        DriverProfileRecord back = DriverProfileRecord.Parse(json)!;
        back.Profile.Should().Be("Clair Obscur: Expedition 33");
        back.ValueOf(NvidiaDriverSettings.DlssSrOverride).Should().Be(1);
        back.ValueOf(NvidiaDriverSettings.SmoothMotion).Should().BeNull("set in no profile");
        back.ValueOf(NvidiaDriverSettings.DlssFgOverride).Should().BeNull("not asked");
        back.Settings[0].Location.Should().Be("Profile");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    public void AColumnThisBuildCannotReadIsNull(string? json) => DriverProfileRecord.Parse(json).Should().BeNull();

    [Fact]
    public void AReadingNeverAskedStoresNothing() => DriverProfileRecord.Serialize(DriverProfileReading.NotRead).Should().BeNull();
}

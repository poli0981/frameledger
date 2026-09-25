using FluentAssertions;
using FrameLedger.Application.Capture;
using FrameLedger.Infrastructure.Telemetry;

namespace FrameLedger.Infrastructure.Tests.Telemetry;

/// <summary>
/// The driver-profile source (beta.8) over a scripted bridge and the real one: a machine without a usable driver is
/// Degraded and never throws; an answered profile keeps each setting's value only where the driver gave one, where it came
/// from, and whether someone set it; the ids asked are <see cref="NvidiaDriverSettings.All"/>; the bridge reference taken
/// is released.
/// </summary>
public sealed class NvapiDriverProfileSourceTests
{
    private const string _exe = @"C:\Games\Title\game.exe";

    [Fact]
    public void ADriverThatDoesNotInitialiseIsDegradedAndNothingIsAsked()
    {
        using var bridge = new FakeNvapiBridge { InitResult = -101 };
        using var source = new NvapiDriverProfileSource(bridge);

        DriverProfileReading r = source.Read(_exe);

        r.Outcome.Should().Be(DriverProfileOutcome.Degraded);
        r.NvapiStatus.Should().Be(-101);
        bridge.ProfilesRead.Should().BeEmpty();
        source.Read(_exe).Outcome.Should().Be(DriverProfileOutcome.Degraded, "the answer is remembered, not asked again");
        bridge.Inits.Should().Be(1);
    }

    [Fact]
    public void AnApplicationProfileKeepsEachValueWhereTheDriverGaveOneAndSaysWhereItCameFrom()
    {
        IReadOnlyList<uint>? asked = null;
        using var bridge = new FakeNvapiBridge
        {
            Profile = (path, ids) =>
            {
                asked = ids;
                return new NvapiDriverProfile
                {
                    Status = NvapiDriverProfile.Application,
                    NvapiStatus = 0,
                    Count = 3,
                    ProfileName = "Clair Obscur: Expedition 33",
                    Settings =
                    [
                        new NvapiProfileSetting { Id = NvidiaDriverSettings.DlssSrOverride, Status = 0, Value = 1, Location = 0, Predefined = 0, Dword = 1 },
                        new NvapiProfileSetting { Id = NvidiaDriverSettings.DlssSrPreset, Status = 0, Value = 11, Location = 1, Predefined = 1, Dword = 1 },
                        new NvapiProfileSetting { Id = NvidiaDriverSettings.SmoothMotion, Status = NvapiProfileSetting.SettingNotFound },
                    ],
                };
            },
        };
        using (var source = new NvapiDriverProfileSource(bridge))
        {
            DriverProfileReading r = source.Read(_exe);

            r.Outcome.Should().Be(DriverProfileOutcome.Application);
            r.ProfileName.Should().Be("Clair Obscur: Expedition 33");
            r.Settings.Should().Equal(
                new DriverSettingReading(NvidiaDriverSettings.DlssSrOverride, 1, DriverSettingLocation.Profile, Predefined: false),
                new DriverSettingReading(NvidiaDriverSettings.DlssSrPreset, 11, DriverSettingLocation.Global, Predefined: true),
                new DriverSettingReading(NvidiaDriverSettings.SmoothMotion, null, DriverSettingLocation.NotSet, Predefined: false));
            asked.Should().Equal(NvidiaDriverSettings.All);
            bridge.ProfilesRead.Should().Equal(_exe);
        }

        bridge.Shutdowns.Should().Be(1, "the one reference taken is released");
        bridge.Disposed.Should().BeTrue();
    }

    [Fact]
    public void AGlobalAnswerAndADegradedOneAreSaidAsSuch()
    {
        var global = NvapiDriverProfileSource.Map(new NvapiDriverProfile { Status = NvapiDriverProfile.Global, NvapiStatus = -166, Count = 0, ProfileName = "Base Profile", Settings = [] });
        global.Outcome.Should().Be(DriverProfileOutcome.Global);
        global.NvapiStatus.Should().Be(-166, "the lookup's status: no profile names the executable");

        var degraded = NvapiDriverProfileSource.Map(new NvapiDriverProfile { Status = NvapiDriverProfile.Degraded, NvapiStatus = -1000 });
        degraded.Outcome.Should().Be(DriverProfileOutcome.Degraded);
        degraded.Settings.Should().BeEmpty();

        var truncated = NvapiDriverProfileSource.Map(new NvapiDriverProfile { Status = NvapiDriverProfile.Global, Count = 5, Settings = [new NvapiProfileSetting { Id = 1, Status = 0, Value = 7, Dword = 1 }] });
        truncated.Settings.Should().ContainSingle("a count past the array is never read past it");
    }

    /// <summary>The real bridge, read-only, on this machine: an NVIDIA driver answers the global profile for this test's own executable; none answers Degraded.</summary>
    [Fact]
    public void TheRealBridgeAnswersForThisProcessOrSaysWhy()
    {
        NativeNvapiBridge.IsPresent.Should().BeTrue("FrameLedger.NvapiBridge.dll must be staged beside the test binary");
        using var source = new NvapiDriverProfileSource();

        DriverProfileReading r = source.Read(Environment.ProcessPath!);

        if (r.Outcome == DriverProfileOutcome.Degraded)
        {
            r.Settings.Should().BeEmpty();
            return;
        }

        r.Outcome.Should().BeOneOf(DriverProfileOutcome.Global, DriverProfileOutcome.Application);
        r.Settings.Select(static s => s.Id).Should().Equal(NvidiaDriverSettings.All, "one answer per id, in the order asked");
    }
}

using System.Globalization;
using System.Text.RegularExpressions;
using FluentAssertions;
using FrameLedger.Infrastructure.Recording;

namespace FrameLedger.Infrastructure.Tests.Recording;

/// <summary>The display producer never throws, and what it reports is a mode or nothing — never a zero.</summary>
public sealed partial class PrimaryDisplayTests
{
    [GeneratedRegex(@"^(?<w>\d+)x(?<h>\d+)$", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex Mode();

    [Fact]
    public void ReadReturnsAWellFormedModeOrNulls()
    {
        (string? resolution, double? hz) = PrimaryDisplay.Read();
        if (resolution is null)
        {
            // A session host with no display (CI) is a snapshot with no display; both columns stay NULL together.
            hz.Should().BeNull();
            return;
        }

        Match m = Mode().Match(resolution);
        m.Success.Should().BeTrue(resolution);
        int.Parse(m.Groups["w"].Value, CultureInfo.InvariantCulture).Should().BePositive();
        int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture).Should().BePositive();
        if (hz is double f)
        {
            f.Should().BeGreaterThan(1, "0 and 1 are DEVMODEW's 'hardware default', which is not a number");
        }
    }

    [Fact]
    public void TheSnapshotSourceCarriesTheDisplayColumns()
    {
        (string? resolution, double? hz) = PrimaryDisplay.Read();
        var snapshot = new HardwareSnapshotSource(new FrameLedger.Infrastructure.Telemetry.DxgiAdapters()).Take();
        snapshot.DisplayRes.Should().Be(resolution);
        snapshot.DisplayHz.Should().Be(hz);
    }
}

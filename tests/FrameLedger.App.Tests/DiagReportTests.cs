using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Application.Settings;

namespace FrameLedger.App.Tests;

/// <summary><c>10_LOGGING</c> §Diagnostics extras: the flag, and a report that names every registry key and never guesses a ledger it could not open.</summary>
public sealed class DiagReportTests
{
    [Fact]
    public void TheFlagIsRecognisedAnywhereInTheArguments()
    {
        DiagReport.Requested(["--diag"]).Should().BeTrue();
        DiagReport.Requested(["x", "--diag"]).Should().BeTrue();
        DiagReport.Requested([]).Should().BeFalse();
        DiagReport.Requested(["--DIAG"]).Should().BeFalse("flags are case-sensitive, like the Agent's");
    }

    [Fact]
    public void TheReportCarriesTheInstallAndEveryKey()
    {
        var now = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["hooking.kill_switch"] = "1" };

        string report = DiagReport.Build("1.2.3", @"C:\data", @"C:\data\logs", 2, values, agentBesideApp: true, now);

        report.Should().StartWith("FrameLedger --diag  2026-09-14T12:00:00.0000000+00:00");
        report.Should().Contain("app_version: 1.2.3").And.Contain("ledger_schema: 2").And.Contain("agent_beside_app: True");
        report.Should().Contain("  hooking.kill_switch = 1");
        foreach (SettingDefinition definition in SettingsRegistry.All)
        {
            report.Should().Contain("  " + definition.Key + " = ", definition.Key);
        }

        report.Should().Contain("  capture.min_session_s = 30", "a key with no row reports its default");
    }

    [Fact]
    public void ALedgerThatCouldNotBeOpenedSaysSo()
    {
        string report = DiagReport.Build("1", "d", "l", null, new Dictionary<string, string>(StringComparer.Ordinal), false, DateTimeOffset.UnixEpoch);

        report.Should().Contain("ledger_schema: could not open");
        DiagReport.FileName(new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero)).Should().MatchRegex(@"^diag-\d{8}-\d{6}\.txt$");
    }
}

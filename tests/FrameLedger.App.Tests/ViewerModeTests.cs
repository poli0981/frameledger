// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.Update;
using FrameLedger.Application.Settings;
using FrameLedger.Shared.Ipc;
using FrameLedger.Shared.Safety;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.15 (D52): a viewer over a copy starts and contacts no Agent and changes nothing on this PC. The composition holds
/// that structurally — nothing that could reach the pipe, start an Agent, write the Run entry, run the Agent's flags or
/// update is registered — and every control that would ask is disabled on its page.
/// </summary>
public sealed class ViewerModeTests
{
    private static readonly UiMode _viewer = new(true, @"C:\copy-of-FrameLedger");

    private sealed class CountingPrompt : IConsentPrompt
    {
        public int Shown { get; private set; }

        public Task<bool> ShowAsync(string gameName, CancellationToken ct = default)
        {
            Shown++;
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task TheViewersLinkIsNeverConnectedAndSendsNothing()
    {
        var link = new ViewerAgentLink();

        link.State.Should().Be(AgentConnectionState.Viewer);
        link.IsConnected.Should().BeFalse();
        link.Hello.Should().BeNull();
        link.Status.Should().BeNull();
        await FluentActions.Awaiting(() => link.RequestAsync(IpcMessageType.SetHookEnabled, new SetHookEnabledRequest(1, true, SafetyDisclosure.Version)))
            .Should().ThrowAsync<InvalidOperationException>();
        AgentStatusPresentation.Pill(AgentConnectionState.Viewer).Text.Should().Be(Strings.Agent_State_Viewer);
        AgentStatusPresentation.Banner(AgentConnectionState.Viewer).Should().BeNull("the viewer's own banner says it; the offline banner's Retry has nothing to retry");
    }

    /// <summary>Rule 1's door: in a viewer the consent dialog never opens, so no acknowledgement can be typed for a request that cannot be sent.</summary>
    [Fact]
    public async Task TheConsentDialogNeverOpensInAViewer()
    {
        var prompt = new CountingPrompt();
        var consent = new HookingConsent(new ViewerAgentLink(), prompt);

        (await consent.EnableAsync(1, "Game", TestContext.Current.CancellationToken)).Outcome.Should().Be(HookingConsentOutcome.AgentUnavailable);
        (await consent.DisableAsync(1, TestContext.Current.CancellationToken)).Outcome.Should().Be(HookingConsentOutcome.AgentUnavailable);
        prompt.Shown.Should().Be(0);
    }

    [Fact]
    public async Task AViewersCompositionHoldsNothingThatReachesPastItsLedger()
    {
        var services = new ServiceCollection();
        MachineFacingServices.Add(services, _viewer);

        services.Should().NotContain(static d => d.ServiceType == typeof(AgentConnection) || d.ServiceType == typeof(IAgentLauncher) || d.ServiceType == typeof(IHostedService),
            "no pipe client, no launcher, and no hosted service: neither the connection's rounds nor the update check run");
        services.Should().NotContain(static d => d.ImplementationType == typeof(RunAtLogonRegistry) || d.ImplementationType == typeof(AgentTool) || d.ImplementationType == typeof(VelopackUpdateClient));

        await using ServiceProvider provider = services.BuildServiceProvider();
        IAgentLink link = provider.GetRequiredService<IAgentLink>();
        link.Should().BeOfType<ViewerAgentLink>();
        provider.GetRequiredService<IAgentRequests>().Should().BeSameAs(link);
        provider.GetRequiredService<IRunAtLogon>().Should().BeOfType<ViewerRunAtLogon>().Which.IsSet.Should().BeFalse();
        provider.GetRequiredService<IUpdateClient>().IsInstalled.Should().BeFalse("a viewer never looks for, downloads or applies an update");
        (await provider.GetRequiredService<IAgentTool>().RunAsync("--install-task", TestContext.Current.CancellationToken)).ExitCode.Should().Be(ViewerAgentTool.ExitViewer);
        provider.GetRequiredService<UiMode>().Should().Be(_viewer);
        provider.GetRequiredService<LiveSessions>().Should().NotBeNull();
    }

    [Fact]
    public void TheProfilesCompositionStillHasTheAgentAndTheMachine()
    {
        var services = new ServiceCollection();
        MachineFacingServices.Add(services, UiMode.Profile);

        services.Should().Contain(static d => d.ServiceType == typeof(AgentConnection));
        services.Should().Contain(static d => d.ServiceType == typeof(IAgentLauncher));
        services.Where(static d => d.ServiceType == typeof(IHostedService)).Select(static d => d.ImplementationType)
            .Should().BeEquivalentTo([typeof(AgentConnectionHostedService), typeof(UpdateHostedService)]);
        services.Should().Contain(static d => d.ImplementationType == typeof(RunAtLogonRegistry));
        services.Should().Contain(static d => d.ImplementationType == typeof(AgentTool));
        services.Should().Contain(static d => d.ImplementationType == typeof(VelopackUpdateClient));
    }

    /// <summary>Every control that asks the Agent or changes this PC binds the viewer's switch, page by page.</summary>
    [Theory]
    [InlineData("MainWindow.xaml", "Command", "UpdateRulesCommand")]
    [InlineData("MainWindow.xaml", "Command", "CheckForUpdatesCommand")]
    [InlineData("Pages/GameDetailPage.xaml", "Command", "GrantExceptionCommand")]
    [InlineData("Pages/GameDetailPage.xaml", "Command", "WithdrawExceptionCommand")]
    [InlineData("Pages/GameDetailPage.xaml", "Command", "ReEnableHookingCommand")]
    [InlineData("Pages/GameDetailPage.xaml", "Command", "DeleteAllSessionsCommand")]
    [InlineData("Pages/SettingsPage.xaml", "Command", "DeleteAllSessionsCommand")]
    [InlineData("Pages/SettingsPage.xaml", "Command", "RefreshExceptionsCommand")]
    [InlineData("Pages/SettingsPage.xaml", "IsChecked", "KillSwitch, Mode=TwoWay")]
    [InlineData("Pages/SettingsPage.xaml", "IsChecked", "RunElevated, Mode=TwoWay")]
    [InlineData("Pages/SettingsPage.xaml", "IsChecked", "StartWithWindows, Mode=TwoWay")]
    [InlineData("Pages/SettingsPage.xaml", "IsChecked", "AutoCheckUpdates, Mode=TwoWay")]
    [InlineData("Pages/SettingsPage.xaml", "SelectedValue", "UpdateChannel, Mode=TwoWay")]
    [InlineData("Pages/SettingsPage.xaml", "ItemsSource", "ExceptionGames")]
    public void EveryControlThatAsksTheAgentOrChangesThisPcIsOffInAViewer(string file, string attribute, string binding)
    {
        ArgumentNullException.ThrowIfNull(file);
        string relative = file.Replace('/', Path.DirectorySeparatorChar);
        XDocument xaml = AccessibilityTests.AppXaml().Single(x => string.Equals(x.File, relative, StringComparison.Ordinal)).Xaml;

        XElement control = xaml.Descendants().Single(e => string.Equals((string?)e.Attribute(attribute), "{Binding ViewModel." + binding + "}", StringComparison.Ordinal));

        ((string?)control.Attribute("IsEnabled")).Should().Be("{Binding ViewModel.ActsOnThisPc}", $"{file}: {binding}");
    }

    /// <summary>
    /// beta.18: every setting the Agent reads is off in a viewer, which has no Agent — the exception option and the three
    /// Recording numbers stayed live, and a change said "Applied. The capture agent reads it at the next session start." The
    /// registry says which settings the Agent reads, so a new one without its control here turns this red.
    /// </summary>
    [Fact]
    public void EverySettingTheAgentReadsIsOffInAViewer()
    {
        Dictionary<string, (string Attribute, string Binding)> controls = new(StringComparer.Ordinal)
        {
            [SettingsRegistry.HookingKillSwitch.Key] = ("IsChecked", "KillSwitch, Mode=TwoWay"),
            [SettingsRegistry.HookingUserModeExceptions.Key] = ("IsChecked", "UserModeExceptions, Mode=TwoWay"),
            [SettingsRegistry.CaptureRunElevated.Key] = ("IsChecked", "RunElevated, Mode=TwoWay"),
            [SettingsRegistry.CaptureMinSessionSeconds.Key] = ("Value", "MinSessionSeconds, Mode=TwoWay"),
            [SettingsRegistry.TelemetryIntervalMs.Key] = ("Value", "TelemetryIntervalMs, Mode=TwoWay"),
            [SettingsRegistry.RetentionRawSessionsPerGame.Key] = ("Value", "RetentionRawSessions, Mode=TwoWay"),
        };
        XDocument xaml = AccessibilityTests.AppXaml().Single(static x => string.Equals(x.File, Path.Combine("Pages", "SettingsPage.xaml"), StringComparison.Ordinal)).Xaml;

        List<string> live = [.. controls
            .Select(c => (Key: c.Key, Control: xaml.Descendants().Single(e => string.Equals((string?)e.Attribute(c.Value.Attribute), "{Binding ViewModel." + c.Value.Binding + "}", StringComparison.Ordinal))))
            .Where(static c => !string.Equals((string?)c.Control.Attribute("IsEnabled"), "{Binding ViewModel.ActsOnThisPc}", StringComparison.Ordinal))
            .Select(static c => c.Key)];

        SettingsRegistry.All.Where(static d => d.AgentReads).Select(static d => d.Key).Should().BeEquivalentTo(controls.Keys, "every setting the Agent reads has its control named here");
        string.Join(", ", live).Should().BeEmpty("a viewer has no Agent to read them");
    }
}

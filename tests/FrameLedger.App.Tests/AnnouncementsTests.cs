// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Xml.Linq;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.Update;
using FrameLedger.Shared.Ipc;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Tests;

/// <summary>
/// What the App tells a screen reader as it appears (beta.35, owner decision D89): a screen reader reads what has the
/// focus, and a safety notice above the page, a message at the bottom of the window or a result line is never there. Each
/// source says it once, in the words it shows; the one class that raises UI Automation's notification is held on its own.
/// </summary>
public sealed class AnnouncementsTests
{
    private sealed class FakeSnackbar : ISnackbarService
    {
        public List<(string Title, string Message, ControlAppearance Appearance)> Shown { get; } = [];

        public TimeSpan DefaultTimeOut { get; set; } = TimeSpan.FromSeconds(4);

        public void SetSnackbarPresenter(SnackbarPresenter contentPresenter)
        {
        }

        public SnackbarPresenter? GetSnackbarPresenter() => null;

        public void Show(string title, string message, ControlAppearance appearance, IconElement? icon, TimeSpan timeout) => Shown.Add((title, message, appearance));
    }

    [Fact]
    public async Task EveryMessageOfTheStripIsSaidAsItIsShownAndAWarningIsAnAlert()
    {
        var snackbar = new FakeSnackbar();
        var said = new RecordingAnnouncer();
        var expected = await PagesLoadTests.OnStaAsync(() =>
        {
            // The strip makes the snackbar's icon, a WPF element: on the STA thread, as the App's dispatcher runs it.
            var strip = new SnackbarStrip(snackbar, said);
            strip.Info("Export", "Saved.");
            strip.Warn("Update rules", "The agent is not connected.");
            strip.Success("Backup", "Done.");
            new SnackbarStrip(new FakeSnackbar()).Info("x", "y"); // with no announcer, as before: shown and nothing said
            return (SpokenText.TitleAndBody("Export", "Saved."), SpokenText.TitleAndBody("Update rules", "The agent is not connected."), SpokenText.TitleAndBody("Backup", "Done."));
        });

        snackbar.Shown.Select(static s => s.Title).Should().Equal("Export", "Update rules", "Backup");
        said.Said.Should().Equal(
            (expected.Item1, AnnouncementKind.Status, "Strip"),
            (expected.Item2, AnnouncementKind.Alert, "Strip"),
            (expected.Item3, AnnouncementKind.Status, "Strip"));
    }

    [Fact]
    public void ASafetyNoticeIsSaidOnceWhenItArrivesAndAnErrorOnlyByTheStrip()
    {
        var link = new FakeAgentLink();
        var strip = new RecordingStrip();
        var said = new RecordingAnnouncer();
        using var notices = new SafetyNotices(link, strip, announcer: said);

        link.Raise(IpcMessageType.CaptureRefused, new CaptureRefusedEvent(7, "Alpha", "GuardBlocked", "Easy Anti-Cheat", "EasyAntiCheat.sys"));
        link.Raise(IpcMessageType.CaptureError, new CaptureErrorEvent(null, "InjectFailed", "boom"));

        SafetyNotice notice = notices.Items.Should().ContainSingle().Subject;
        said.Said.Should().ContainSingle().Which.Should().Be((notice.AccessibleName, AnnouncementKind.Alert, "Notice"),
            "every notice is read — a refusal is the one thing a user must not miss");
        strip.Shown.Should().ContainSingle("a capture error is the strip's, and the strip says it");
    }

    [Fact]
    public void TheShellSaysOnlyAChangeOfTheAgentAndOfAnUpdatesStep()
    {
        var said = new RecordingAnnouncer();
        var shell = new ShellAnnouncements(said);

        shell.Agent(AgentConnectionState.Connecting, null);
        shell.Agent(AgentConnectionState.Connected, null);
        said.Said.Should().BeEmpty("the first state is read with the window, and connecting → connected shows no banner");

        shell.Agent(AgentConnectionState.Offline, "not answering");
        shell.Agent(AgentConnectionState.Offline, "not answering");
        shell.Agent(AgentConnectionState.Connected, null);
        said.Said.Select(static s => s.Kind).Should().Equal(AnnouncementKind.Alert, AnnouncementKind.Status);
        said.Said[0].Text.Should().EndWith("not answering");
        said.Said[1].Text.Should().Be(Strings.A11y_Agent_Connected);

        said.Said.Clear();
        shell.Update(UpdateStage.Idle, null, string.Empty);
        shell.Update(UpdateStage.Downloading, "0.1.0-beta.36", "Downloading… 1%");
        shell.Update(UpdateStage.Downloading, "0.1.0-beta.36", "Downloading… 40%");
        shell.Update(UpdateStage.Ready, "0.1.0-beta.36", "Ready to install.");
        shell.Update(UpdateStage.Ready, "0.1.0-beta.36", "Ready to install.");
        said.Said.Select(static s => s.Activity).Should().Equal("Update", "Update");
        said.Said[0].Text.Should().Contain("0.1.0-beta.36").And.NotContain("%", "the percent ticks are one step");
        said.Said[1].Text.Should().EndWith("Ready to install.");
    }

    /// <summary>The one place that raises the notification: only when someone listens, only on the App's active window.</summary>
    [Fact]
    public async Task UiAutomationsNotificationIsRaisedOnTheTargetWindowOnlyWhenAScreenReaderListens()
    {
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            var window = new Window();
            var raised = new List<(AutomationPeer Peer, AutomationNotificationProcessing Processing, string Text, string Activity)>();
            void Raise(AutomationPeer peer, AutomationNotificationKind kind, AutomationNotificationProcessing processing, string text, string activity) => raised.Add((peer, processing, text, activity));

            new UiaAnnouncer(() => window, static () => false, Raise).Announce("unheard", AnnouncementKind.Alert, "Notice");
            new UiaAnnouncer(static () => null, static () => true, Raise).Announce("nowhere", AnnouncementKind.Alert, "Notice");
            var announcer = new UiaAnnouncer(() => window, static () => true, Raise);
            announcer.Announce("  ", AnnouncementKind.Status, "Strip");
            announcer.Announce("Alpha: hooking refused.", AnnouncementKind.Alert, "Notice");
            announcer.Announce("Saved.", AnnouncementKind.Status, "Strip");

            // The App's own: Windows' check and the peer's real event, on a window nobody shows — nothing listens here, nothing throws.
            new UiaAnnouncer().Announce("quiet", AnnouncementKind.Status, "Strip");
            UIElementAutomationPeer.CreatePeerForElement(window).RaiseNotificationEvent(AutomationNotificationKind.Other, AutomationNotificationProcessing.MostRecent, "x", "FrameLedger.Test");
            window.Close();
            return raised.Select(static r => (r.Peer is WindowAutomationPeer, r.Processing, r.Text, r.Activity)).ToList();
        });

        facts.Should().Equal(
            (true, AutomationNotificationProcessing.ImportantAll, "Alpha: hooking refused.", "FrameLedger.Notice"),
            (true, AutomationNotificationProcessing.MostRecent, "Saved.", "FrameLedger.Strip"));
    }

    [Fact]
    public async Task AnAnnouncementFromAnotherThreadIsRaisedOnTheWindowsThread()
    {
        var raised = new List<int>();
        Window? window = null;
        UiaAnnouncer announcer = await PagesLoadTests.OnStaAsync(() =>
        {
            window = new Window();
            return new UiaAnnouncer(() => window, static () => true, (_, _, _, _, _) => raised.Add(Environment.CurrentManagedThreadId));
        });
        int sta = await PagesLoadTests.OnStaAsync(static () => Environment.CurrentManagedThreadId);

        await Task.Run(() => announcer.Announce("from the pipe", AnnouncementKind.Status, "Agent"), TestContext.Current.CancellationToken);
        await PagesLoadTests.OnStaAsync(() =>
        {
            Visuals.Pump();
            window!.Close();
            return 0;
        });

        raised.Should().Equal(sta);
    }

    /// <summary>Every transient message goes through the strip, which says it; a LiveSetting would promise speech WPF never gives.</summary>
    [Fact]
    public void OnlyTheStripShowsASnackbarAndNoXamlSetsALiveSetting()
    {
        string app = Path.Combine(AccessibilityTests.RepoRoot(), "src", "FrameLedger.App");
        string[] allowed = ["SnackbarStrip.cs", "MainWindow.xaml.cs", "App.xaml.cs"];
        IEnumerable<string> sources = Directory.EnumerateFiles(app, "*.cs", SearchOption.AllDirectories)
            .Where(static f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal));

        sources.Where(f => File.ReadAllText(f).Contains("ISnackbarService", StringComparison.Ordinal) && !allowed.Contains(Path.GetFileName(f), StringComparer.Ordinal))
            .Select(f => Path.GetRelativePath(app, f)).Should().BeEmpty("a snackbar shown past the strip is never said (D89)");
        AccessibilityTests.AppXaml().Where(static x => x.Xaml.Descendants().Attributes().Any(static a => string.Equals(a.Name.LocalName, "AutomationProperties.LiveSetting", StringComparison.Ordinal)))
            .Select(static x => x.File).Should().BeEmpty("WPF raises a live region only when the app does; the App says a change through IAnnouncer");
    }
}

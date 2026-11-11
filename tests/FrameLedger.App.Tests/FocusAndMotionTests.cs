// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using FluentAssertions;
using FrameLedger.App.Services;
using Wpf.Ui;
using Wpf.Ui.Animations;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Tests;

/// <summary>
/// Focus and motion (beta.35): a page slides in only while Windows' animation effects are on, and Ctrl+1 … Ctrl+5 put the
/// keyboard in the page they open, its title said.
/// </summary>
public sealed partial class FocusAndMotionTests
{
    [GeneratedRegex(@"\bShowDialogAsync\(", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex BoxShown();

    [Fact]
    public void APageSlidesInOnlyWhileWindowsAnimatesAndFollowsTheSettingAsItChanges()
    {
        PageMotion.For(animations: true).Should().Be(Transition.FadeInWithSlide, "WPF UI's own, as before");
        PageMotion.For(animations: false).Should().Be(Transition.None);
    }

    [Fact]
    public async Task TheNavigationTakesTheTransitionOfTheSettingNowAndAgainWhenItChanges()
    {
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            bool animations = false;
            var navigation = new NavigationView();
            Transition before = navigation.Transition;
            using PageMotion motion = PageMotion.Follow(navigation, () => animations);
            Transition off = navigation.Transition;
            animations = true;
            motion.Apply();
            return (before, off, On: navigation.Transition);
        });

        facts.before.Should().Be(Transition.FadeInWithSlide, "WPF UI 4.3.0's default, played whatever Windows says");
        facts.off.Should().Be(Transition.None);
        facts.On.Should().Be(Transition.FadeInWithSlide);
    }

    [Fact]
    public async Task AShortcutPutsTheFocusOnThePagesFirstControlAndSaysItsTitle()
    {
        var said = new RecordingAnnouncer();
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            var focus = new PageFocus(said);
            var elsewhere = new System.Windows.Controls.Button { Content = "Elsewhere" };
            var heading = new System.Windows.Controls.TextBlock { Text = "Games" };
            AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level1);
            var first = new System.Windows.Controls.Button { Content = "Add game" };
            var page = new StackPanel { Children = { heading, first, new System.Windows.Controls.Button { Content = "Import" } } };
            var root = new StackPanel { Children = { elsewhere, page } };
            var host = new FluentWindow { Content = root, Width = 300, Height = 300, ShowActivated = true, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
            host.Show();
            try
            {
                Visuals.Pump();
                _ = elsewhere.Focus();
                focus.Shown(page);
                focus.Request();
                Visuals.Pump();
                return (Focused: Keyboard.FocusedElement, first, Title: PageFocus.Title(page));
            }
            finally
            {
                host.Close();
            }
        });

        facts.Focused.Should().BeSameAs(facts.first, "the page's first control has the keyboard");
        facts.Title.Should().Be("Games");
        said.Said.Should().ContainSingle().Which.Should().Be(("Games", AnnouncementKind.Status, "Page"));
    }

    [Fact]
    public async Task APageNotYetShownTakesTheFocusOnceItIs()
    {
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            var focus = new PageFocus();
            var first = new System.Windows.Controls.Button { Content = "Add game" };
            var page = new StackPanel { Children = { first } };
            focus.Shown(page);
            focus.Request();
            var host = new FluentWindow { Content = page, Width = 300, Height = 300, ShowActivated = true, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
            host.Show();
            try
            {
                Visuals.Pump();
                return (Focused: Keyboard.FocusedElement, first);
            }
            finally
            {
                host.Close();
            }
        });

        facts.Focused.Should().BeSameAs(facts.first);
    }

    /// <summary>
    /// A content dialog gives the keyboard back to the control that opened it — WPF UI 4.3.0's host stores the focused element
    /// when the dialog opens and focuses it again when it closes, so the App adds nothing; held here so an update that drops it
    /// goes red. (The dialog's own <c>finally</c>, which takes it out of its host, resumes on the dispatcher's context: without
    /// one, as on a bare test thread, the dialog stays in its host and the focus on its Close button.)
    /// </summary>
    [Fact]
    public async Task AContentDialogGivesTheFocusBackToTheControlThatOpenedIt()
    {
        var facts = await PagesLoadTests.OnStaAsync(() => OnDispatcherContext(() =>
        {
            (FluentWindow window, Wpf.Ui.Controls.Button opener, ContentDialogService service) = Shell();
            try
            {
                _ = opener.Focus();
                Close(service.ShowAsync(new ContentDialog { Title = "T", Content = "Body", CloseButtonText = "Close" }, CancellationToken.None), service);
                return (Focused: Keyboard.FocusedElement, opener);
            }
            finally
            {
                window.Close();
            }
        }));

        facts.Focused.Should().BeSameAs(facts.opener);
    }

    [Fact]
    public async Task AMessageBoxIsOwnedByTheAppsWindowItOpensOver()
    {
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            var window = new FluentWindow { Width = 300, Height = 200, ShowActivated = true, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
            Window? main = System.Windows.Application.Current.MainWindow;
            window.Show();
            System.Windows.Application.Current.MainWindow = window;
            try
            {
                var box = new Wpf.Ui.Controls.MessageBox { Title = "T", Content = "C", CloseButtonText = "Close" };
                Window? before = box.Owner;
                MessageBoxes.Own(box);
                return (before, After: box.Owner, window);
            }
            finally
            {
                System.Windows.Application.Current.MainWindow = main;
                window.Close();
            }
        });

        facts.before.Should().BeNull("WPF UI 4.3.0's message box sets no owner");
        facts.After.Should().BeSameAs(facts.window);
    }

    /// <summary>Every message box the App shows goes through <see cref="MessageBoxes"/>: no other code shows one.</summary>
    [Fact]
    public void EveryMessageBoxGoesThroughMessageBoxes()
    {
        string app = Path.Combine(AccessibilityTests.RepoRoot(), "src", "FrameLedger.App");
        List<string> elsewhere = [.. Directory.EnumerateFiles(app, "*.cs", SearchOption.AllDirectories)
            .Where(static f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                               && !f.EndsWith("MessageBoxes.cs", StringComparison.Ordinal))
            .Where(static f => BoxShown().IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(app, f))];

        elsewhere.Should().BeEmpty("a box shown elsewhere has no owner, and can fall behind the window it came from");
    }

    /// <summary>
    /// The work under the STA thread's own dispatcher context: WPF UI's <c>ContentDialog.ShowAsync</c> clears its host in a
    /// <c>finally</c> after an await, which must resume on the dispatcher, as it does in the App.
    /// </summary>
    private static T OnDispatcherContext<T>(Func<T> work)
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
        try
        {
            return work();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static (FluentWindow Window, Wpf.Ui.Controls.Button Opener, ContentDialogService Service) Shell()
    {
        var opener = new Wpf.Ui.Controls.Button { Content = "Open" };
        var host = new ContentDialogHost();
        var grid = new Grid();
        grid.Children.Add(new StackPanel { Children = { new Wpf.Ui.Controls.Button { Content = "Other" }, opener } });
        grid.Children.Add(host);
        var window = new FluentWindow { Content = grid, Width = 500, Height = 400, ShowActivated = true, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
        window.Show();
        Visuals.Pump();
        var service = new ContentDialogService();
        service.SetDialogHost(host);
        return (window, opener, service);
    }

    // The dialog open, then closed with no answer, and the dispatcher run until its task has finished.
    private static void Close(Task<ContentDialogResult> shown, ContentDialogService service)
    {
        Visuals.Pump();
        (service.GetDialogHostEx()?.Content as ContentDialog)?.Hide(ContentDialogResult.None);
        for (int i = 0; i < 200 && !shown.IsCompleted; i++)
        {
            Visuals.Pump();
            Thread.Sleep(5);
        }

        shown.IsCompleted.Should().BeTrue("the dialog closed");
        Visuals.Pump();
    }
}

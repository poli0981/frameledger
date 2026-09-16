using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml.Linq;
using FluentAssertions;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Tests;

/// <summary>
/// NFR-9 as <c>08_UI</c> §Accessibility states it (P4 PR-8), each clause held by a check that goes red on a regression:
/// every input that shows no text of its own carries an automation name, no XAML hard-codes a colour (contrast is the
/// Fluent themes'), the manifest is Per-Monitor V2, Ctrl+1…5 follow the NavigationView's order, and Esc closes the open
/// dialog with no result.
/// </summary>
public sealed class AccessibilityTests
{
    private static readonly XNamespace _x = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>The input controls a screen reader announces by name; a textless one without a name is announced as its type alone.</summary>
    private static readonly HashSet<string> _namedControls = new(StringComparer.Ordinal)
    {
        "Button", "ToggleButton", "ToggleSwitch", "CheckBox", "RadioButton", "ComboBox", "TextBox", "NumberBox", "PasswordBox", "Slider", "HyperlinkButton",
    };

    private static readonly HashSet<string> _colourProperties = new(StringComparer.Ordinal)
    {
        "Foreground", "Background", "BorderBrush", "Fill", "Stroke", "Color", "CaretBrush", "SelectionBrush",
    };

    private static string RepoRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "FrameLedger.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("the repository root (FrameLedger.slnx) is not above " + AppContext.BaseDirectory);
    }

    internal static IEnumerable<(string File, XDocument Xaml)> AppXaml()
    {
        string app = Path.Combine(RepoRoot(), "src", "FrameLedger.App");
        foreach (string file in Directory.EnumerateFiles(app, "*.xaml", SearchOption.AllDirectories)
                     .Where(static f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                                        && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
        {
            yield return (Path.GetRelativePath(app, file), XDocument.Load(file));
        }
    }

    private static bool HasVisibleText(XElement element)
    {
        if (element.Attribute("Content") is not null || element.Attribute("Header") is not null)
        {
            return true;
        }

        // Content as a child element — anything that is not a property element such as <ui:Button.Icon>.
        return element.Elements().Any(child => !child.Name.LocalName.Contains('.', StringComparison.Ordinal));
    }

    private static bool HasAutomationName(XElement element) =>
        element.Attributes().Any(static a => a.Name.LocalName is "AutomationProperties.Name" or "AutomationProperties.LabeledBy");

    [Fact]
    public void EveryInputThatShowsNoTextHasAnAutomationName()
    {
        var unnamed = new List<string>();
        foreach ((string file, XDocument xaml) in AppXaml())
        {
            foreach (XElement element in xaml.Descendants().Where(e => _namedControls.Contains(e.Name.LocalName)))
            {
                if (!HasVisibleText(element) && !HasAutomationName(element))
                {
                    string where = element.Attribute(_x + "Name")?.Value ?? string.Join(" ", element.Attributes().Where(static a => a.Name.LocalName is "Command" or "IsChecked" or "Text" or "SelectedValue" or "Value").Select(static a => a.Value));
                    unnamed.Add($"{file}: <{element.Name.LocalName}> {where}");
                }
            }
        }

        unnamed.Should().BeEmpty("08_UI §Accessibility: a control with no text of its own is announced by AutomationProperties.Name, from .resx");
    }

    [Fact]
    public void NoXamlHardCodesAColour()
    {
        HashSet<string> named = new(typeof(System.Windows.Media.Colors).GetProperties().Select(static p => p.Name), StringComparer.OrdinalIgnoreCase);
        named.Remove("Transparent");
        var hex = new Regex("^#[0-9A-Fa-f]{3,8}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var found = new List<string>();
        foreach ((string file, XDocument xaml) in AppXaml())
        {
            foreach (XAttribute attribute in xaml.Descendants().Attributes().Where(a => _colourProperties.Contains(a.Name.LocalName)))
            {
                if (hex.IsMatch(attribute.Value) || named.Contains(attribute.Value))
                {
                    found.Add($"{file}: {attribute.Parent!.Name.LocalName}.{attribute.Name.LocalName}=\"{attribute.Value}\"");
                }
            }
        }

        found.Should().BeEmpty("08_UI §Accessibility, contrast: colours come from the WPF UI theme dictionaries, whose Fluent themes meet contrast in both modes");
    }

    [Fact]
    public void TheManifestIsPerMonitorV2()
    {
        string manifest = File.ReadAllText(Path.Combine(RepoRoot(), "src", "FrameLedger.App", "app.manifest"));

        manifest.Should().Contain("PerMonitorV2", "NFR-9: Per-Monitor V2 DPI awareness");
    }

    private sealed class RecordingNavigator : IPageNavigator
    {
        public List<string> Pages { get; } = [];

        public void Navigate<TPage>()
            where TPage : class => Pages.Add(typeof(TPage).Name);

        public void GoBack()
        {
        }
    }

    [Fact]
    public void CtrlOneToFiveOpenTheNavigationViewsPagesInItsOrder()
    {
        XDocument shell = XDocument.Load(Path.Combine(RepoRoot(), "src", "FrameLedger.App", "MainWindow.xaml"));
        string[] navigationOrder = [.. shell.Descendants().Where(static e => string.Equals(e.Name.LocalName, "NavigationViewItem", StringComparison.Ordinal))
            .Select(static e => e.Attribute("TargetPageType")?.Value ?? string.Empty)
            .Select(static v => Regex.Match(v, @"pages:(?<page>\w+)\}", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["page"].Value)];
        var bindings = shell.Descendants().Where(static e => string.Equals(e.Name.LocalName, "KeyBinding", StringComparison.Ordinal))
            .Select(static e => (Key: e.Attribute("Key")?.Value, Modifiers: e.Attribute("Modifiers")?.Value, Parameter: e.Attribute("CommandParameter")?.Value, Command: e.Attribute("Command")?.Value))
            .ToList();
        var navigator = new RecordingNavigator();

        for (int i = 1; i <= 5; i++)
        {
            string parameter = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            bindings.Should().Contain((Key: "D" + parameter, Modifiers: "Control", Parameter: parameter, Command: "{Binding ViewModel.NavigateToCommand}"));
            bindings.Should().Contain((Key: "NumPad" + parameter, Modifiers: "Control", Parameter: parameter, Command: "{Binding ViewModel.NavigateToCommand}"));
            ShellShortcuts.Navigate(navigator, parameter).Should().BeTrue();
        }

        navigator.Pages.Should().Equal(navigationOrder);
        navigationOrder.Should().Equal(nameof(DashboardPage), nameof(GamesPage), nameof(ComparePage), nameof(LogsPage), nameof(SettingsPage));
        ShellShortcuts.Navigate(navigator, "6").Should().BeFalse();
        ShellShortcuts.Navigate(navigator, null).Should().BeFalse();
    }

    /// <summary>
    /// An STA thread with a running dispatcher: <c>ContentDialog.ShowAsync</c> clears its host in a <c>finally</c> that
    /// resumes on the captured context, so the thread that showed the dialog must still be pumping when it closes.
    /// </summary>
    private static Task<T> OnDispatcherAsync<T>(Func<Task<T>> work)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            System.Windows.Threading.Dispatcher dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
#pragma warning disable VSTHRD001 // a test's own dispatcher thread: no JoinableTaskFactory exists here, and this posts the work rather than blocking on it
            _ = dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    tcs.SetResult(await work().ConfigureAwait(true));
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    tcs.SetException(ex);
                }
                finally
                {
                    dispatcher.InvokeShutdown();
                }
            });
#pragma warning restore VSTHRD001
            System.Windows.Threading.Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }

    [Fact]
    public async Task EscapeClosesTheOpenDialogWithNoResult()
    {
        (ContentDialogResult Result, bool EmptyHost) outcome = await OnDispatcherAsync(async () =>
        {
            var host = new ContentDialogHost();
            bool closedNothing = !DialogKeyboard.CloseOpenDialog(host);
            var dialog = new ContentDialog(host) { Title = "t", PrimaryButtonText = "accept", CloseButtonText = "close" };
            Task<ContentDialogResult> shown = dialog.ShowAsync();

            DialogKeyboard.CloseOpenDialog(host).Should().BeTrue();
            ContentDialogResult result = await shown.ConfigureAwait(true);

            // WPF UI's CloseButtonText defaults to "Close"; a dialog without one says so with an empty text.
            var answerOnly = new ContentDialog(host) { Title = "t", PrimaryButtonText = "accept", CloseButtonText = string.Empty };
            Task<ContentDialogResult> mustAnswer = answerOnly.ShowAsync();
            DialogKeyboard.CloseOpenDialog(host).Should().BeFalse("a dialog with no Close button is not dismissed by Esc");
            answerOnly.Hide(ContentDialogResult.Primary);
            (await mustAnswer.ConfigureAwait(true)).Should().Be(ContentDialogResult.Primary);

            return (result, closedNothing);
        });

        outcome.Result.Should().Be(ContentDialogResult.None, "Esc is the Close button, never the primary one — the consent dialog and the Legal Gate read None as a refusal");
        outcome.EmptyHost.Should().BeTrue("with no dialog open, Esc is left to whoever else wants it");
    }
}

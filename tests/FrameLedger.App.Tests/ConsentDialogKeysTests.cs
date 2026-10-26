// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using FluentAssertions;
using FrameLedger.App.Dialogs;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.30 (the owner's report, 2026-10-26: the phrase typed, Enter, and hooking stayed off while the switch showed on), on
/// the real controls under the real dictionaries: Enter in the consent dialog enables once the phrase is typed exactly and
/// does nothing before it (owner decision D69), and a OneWay switch whose command changed nothing drops the click's value
/// when its property is raised unchanged — what <c>GameDetailViewModel</c> now does after every switch command.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class ConsentDialogKeysTests
{
    /// <summary>A switch a test can click the way the mouse does: <c>OnToggle</c> flips IsChecked through SetCurrentValue.</summary>
    private sealed class ClickableSwitch : ToggleSwitch
    {
        public void ClickLikeTheMouse() => OnToggle();
    }

    /// <summary>A row that stays off whatever is clicked, and the view model's "raise it whatever happened".</summary>
    private sealed class OffRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public bool On { get; }

        public void Raise() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(On)));
    }

    /// <summary>The shell's dialog service, answering at once: the phrase typed (or not) as a user would, then the button.</summary>
    private sealed class AnsweringDialogs(ContentDialogResult answer, string? typed) : Wpf.Ui.IContentDialogService
    {
        public ContentDialog? Shown { get; private set; }

        public Task<ContentDialogResult> ShowAsync(ContentDialog dialog, CancellationToken cancellationToken)
        {
            Shown = dialog;
            if (typed is not null)
            {
                ((ConsentDialogContent)dialog.Content).ViewModel.Typed = typed;
            }

            return Task.FromResult(answer);
        }

        public void SetDialogHost(ContentDialogHost dialogHost) => throw new NotSupportedException();

        public ContentDialogHost? GetDialogHostEx() => null;

        [Obsolete("WPF UI's own obsolete member, not called")]
        public void SetContentPresenter(System.Windows.Controls.ContentPresenter contentPresenter) => throw new NotSupportedException();

        [Obsolete("WPF UI's own obsolete member, not called")]
        public System.Windows.Controls.ContentPresenter? GetContentPresenter() => null;

        [Obsolete("WPF UI's own obsolete member, not called")]
        public void SetDialogHost(System.Windows.Controls.ContentPresenter dialogHost) => throw new NotSupportedException();

        [Obsolete("WPF UI's own obsolete member, not called")]
        public System.Windows.Controls.ContentPresenter? GetDialogHost() => null;
    }

    /// <summary>What the dialog's template and content do with the keys, read at one moment.</summary>
    private readonly record struct Keys(bool PrimaryIsDefault, bool PrimaryIsEnabled, Visibility PrimaryVisibility, bool CloseIsDefault, bool EnterSwallowed, bool ImeSwallowed);

    [Fact]
    public async Task AOneWaySwitchDropsTheClickWhenItsPropertyIsRaisedUnchanged()
    {
        (bool? clicked, bool? shown) = await PagesLoadTests.OnStaAsync(() =>
        {
            var row = new OffRow();
            var toggle = new ClickableSwitch();
            _ = toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(OffRow.On)) { Source = row, Mode = BindingMode.OneWay });
            Visuals.Layout(toggle, 80, 40);
            toggle.ClickLikeTheMouse();
            bool? afterClick = toggle.IsChecked;
            row.Raise();
            return (afterClick, toggle.IsChecked);
        });

        clicked.Should().BeTrue("a click flips the switch before its command has run");
        shown.Should().BeFalse("raised unchanged, the OneWay binding reads the row again and drops the click's value");
    }

    [Fact]
    public async Task EnterEnablesOnceThePhraseIsTypedAndDoesNothingBefore()
    {
        CultureInfo? previous = Shared.Strings.Culture;
        Shared.Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            (ContentDialogButton defaultButton, Keys before, Keys after) = await PagesLoadTests.OnStaAsync(() =>
            {
                var viewModel = new ConsentDialogViewModel("Alpha");
                ContentDialog dialog = ConsentPrompt.Build(viewModel);
                Visuals.Layout(dialog, 900, 700);
                _ = dialog.ApplyTemplate();
                var content = (ConsentDialogContent)dialog.Content;
                using var source = new HwndSource(new HwndSourceParameters("consent-keys"));

                Keys Read()
                {
                    var primary = (System.Windows.Controls.Button)dialog.Template.FindName("PrimaryButton", dialog);
                    var close = (System.Windows.Controls.Button)dialog.Template.FindName("CloseButton", dialog);
                    return new Keys(primary.IsDefault, primary.IsEnabled, primary.Visibility, close.IsDefault,
                        Swallowed(content, source, Key.Enter), Swallowed(content, source, Key.ImeProcessed));
                }

                Keys typing = Read();
                viewModel.Typed = "I ACCEPT THE INJECTION RISK";
                Visuals.Layout(dialog, 900, 700);
                return (dialog.DefaultButton, typing, Read());
            });

            defaultButton.Should().Be(ContentDialogButton.Primary, "D69: Enter is the primary button's once it is enabled");
            before.PrimaryIsEnabled.Should().BeFalse();
            before.PrimaryVisibility.Should().Be(Visibility.Collapsed, "WPF UI 4.3.0 hides a disabled primary button");
            before.CloseIsDefault.Should().BeTrue("the template makes the close button the default while the primary is disabled");
            before.EnterSwallowed.Should().BeTrue("so the content keeps Enter from it: before the phrase, Enter does nothing");
            before.ImeSwallowed.Should().BeFalse("an IME committing the phrase's diacritics is never taken for Enter");
            after.Should().Be(new Keys(PrimaryIsDefault: true, PrimaryIsEnabled: true, Visibility.Visible, CloseIsDefault: false, EnterSwallowed: false, ImeSwallowed: false),
                "the phrase typed exactly: Enter reaches the enabled primary button, the default, as a click would");
        }
        finally
        {
            Shared.Strings.Culture = previous;
        }
    }

    /// <summary>
    /// What <see cref="ConsentPrompt.ShowAsync"/> makes of the dialog's answer: enabling needs the primary button AND the
    /// phrase — the binding is the gate on the button, and the prompt reads the same rule once more when the dialog closes.
    /// </summary>
    [Theory]
    [InlineData(ContentDialogResult.Primary, true, true)]
    [InlineData(ContentDialogResult.Primary, false, false)]
    [InlineData(ContentDialogResult.None, true, false)]
    public async Task OnlyThePrimaryButtonWithThePhraseTypedEnables(ContentDialogResult answer, bool typed, bool enabled)
    {
        CultureInfo? previous = Shared.Strings.Culture;
        Shared.Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            (bool result, ContentDialogButton defaultButton) = await PagesLoadTests.OnStaAsync(() =>
            {
                var dialogs = new AnsweringDialogs(answer, typed ? "I ACCEPT THE INJECTION RISK" : null);
#pragma warning disable VSTHRD002 // the fake answers synchronously, so the prompt's task has completed on this thread
                bool shown = new ConsentPrompt(dialogs).ShowAsync("Alpha").GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
                return (shown, dialogs.Shown!.DefaultButton);
            });

            result.Should().Be(enabled);
            defaultButton.Should().Be(ContentDialogButton.Primary);
        }
        finally
        {
            Shared.Strings.Culture = previous;
        }
    }

    [Theory]
    [InlineData(Key.Enter, false, true)]
    [InlineData(Key.Enter, true, false)]
    [InlineData(Key.Escape, false, false)]
    [InlineData(Key.ImeProcessed, false, false)]
    [InlineData(Key.A, false, false)]
    public void OnlyAnEnterBeforeThePhraseIsKept(Key key, bool acknowledged, bool swallowed) =>
        ConsentDialogContent.SwallowsEnter(key, acknowledged).Should().Be(swallowed);

    /// <summary>Whether the content handled a key pressed in the phrase box, as the keyboard raises it.</summary>
    private static bool Swallowed(ConsentDialogContent content, PresentationSource source, Key key)
    {
        var e = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        content.Acknowledgement.RaiseEvent(e);
        return e.Handled;
    }
}

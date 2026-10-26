// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows.Controls;
using System.Windows.Input;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Dialogs;

/// <summary>The body of FR-2.1's <c>ContentDialog</c>; the buttons are the dialog's own (<see cref="Services.ConsentPrompt"/>).</summary>
public partial class ConsentDialogContent : UserControl
{
    public ConsentDialogContent(ConsentDialogViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => Acknowledgement.Focus();
        PreviewKeyDown += (_, e) => e.Handled |= SwallowsEnter(e.Key, ViewModel.IsAcknowledged);
    }

    public ConsentDialogViewModel ViewModel { get; }

    /// <summary>
    /// Whether the content keeps Enter from the dialog (beta.30, owner decision D69): while the phrase does not match, the
    /// template makes the close button the default, and an Enter in the box declined without a word. A handled key reaches
    /// no default button, so before the phrase Enter does nothing; once it matches, Enter passes to the primary button —
    /// the default — and enables, as a click does. An IME committing a composition arrives as <see cref="Key.ImeProcessed"/>,
    /// never as Enter, and is left alone.
    /// </summary>
    internal static bool SwallowsEnter(Key key, bool acknowledged) => key == Key.Enter && !acknowledged;
}

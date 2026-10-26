// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows.Data;
using FrameLedger.App.Dialogs;
using FrameLedger.App.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>
/// FR-2.1's dialog in WPF UI (<c>16_WPFUI_SYNTAX</c> §Dialogs: <c>IContentDialogService</c> over the shell's
/// <c>ContentDialogHost</c>). The primary button is bound to the typed acknowledgement and is disabled — WPF UI 4.3.0 also
/// hides it — until the phrase matches. Since beta.30 (owner decision D69) it is the default button: once the phrase is
/// typed exactly, Enter enables, as a click on it does; before that the content swallows Enter
/// (<see cref="ConsentDialogContent.SwallowsEnter"/>), because the template makes Close the default while the primary
/// is disabled, and Enter used to keep hooking off without a word. Esc and the close button keep it off.
/// </summary>
public sealed class ConsentPrompt : IConsentPrompt
{
    private readonly IContentDialogService _dialogs;

    public ConsentPrompt(IContentDialogService dialogs) => _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

    public async Task<bool> ShowAsync(string gameName, CancellationToken ct = default)
    {
        var viewModel = new ConsentDialogViewModel(gameName);
        ContentDialog dialog = Build(viewModel);

        ContentDialogResult result = await _dialogs.ShowAsync(dialog, ct).ConfigureAwait(true);
        // The binding is the gate on the button; this is the same rule read once more at the moment that matters.
        return result == ContentDialogResult.Primary && viewModel.IsAcknowledged;
    }

    /// <summary>The dialog <see cref="ShowAsync"/> shows: the disclosure, the typed acknowledgement, the two buttons.</summary>
    internal static ContentDialog Build(ConsentDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        var dialog = new ContentDialog
        {
            Title = viewModel.Title,
            Content = new ConsentDialogContent(viewModel),
            PrimaryButtonText = ConsentDialogViewModel.EnableText,
            CloseButtonText = ConsentDialogViewModel.KeepOffText,
            // D69: Enter enables once the phrase matches (the primary is the default only while it is enabled).
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
            DialogMaxWidth = 700,
        };
        _ = dialog.SetBinding(ContentDialog.IsPrimaryButtonEnabledProperty, new Binding(nameof(ConsentDialogViewModel.IsAcknowledged)) { Source = viewModel, Mode = BindingMode.OneWay });
        return dialog;
    }
}

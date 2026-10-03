// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows.Data;
using FrameLedger.App.Dialogs;
using FrameLedger.App.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>
/// The admin mode's disclosure in WPF UI, the same shape as D33's (<see cref="AntiCheatExceptionPrompt"/>): the primary button
/// is bound to the ticked acceptance and disabled until it is ticked; the close button keeps standard rights and is the
/// default, so Enter never turns the mode on.
/// </summary>
public sealed class AgentAdminPrompt : IAgentAdminPrompt
{
    private readonly IContentDialogService _dialogs;

    public AgentAdminPrompt(IContentDialogService dialogs) => _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

    public async Task<bool> ShowAsync(CancellationToken ct = default)
    {
        var viewModel = new AgentAdminDialogViewModel();
        var dialog = new ContentDialog
        {
            Title = AgentAdminDialogViewModel.Title,
            Content = new AgentAdminDialogContent(viewModel),
            PrimaryButtonText = AgentAdminDialogViewModel.TurnOnText,
            CloseButtonText = AgentAdminDialogViewModel.CancelText,
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            DialogMaxWidth = 700,
        };
        _ = dialog.SetBinding(ContentDialog.IsPrimaryButtonEnabledProperty,
            new Binding(nameof(AgentAdminDialogViewModel.Accepted)) { Source = viewModel, Mode = BindingMode.OneWay });

        ContentDialogResult result = await _dialogs.ShowAsync(dialog, ct).ConfigureAwait(true);
        return result == ContentDialogResult.Primary && viewModel.Accepted;
    }
}

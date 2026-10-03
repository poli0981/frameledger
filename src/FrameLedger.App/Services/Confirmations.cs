// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary><see cref="IConfirmations"/> over <c>Wpf.Ui.Controls.MessageBox</c> (<c>16_WPFUI_SYNTAX</c> §Dialogs).</summary>
public sealed class Confirmations : IConfirmations
{
    [SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture, which changes at runtime")]
    public async Task<RemoveGameChoice> RemoveGameAsync(string gameName, CancellationToken ct = default)
    {
        var box = new MessageBox
        {
            Title = string.Format(CultureInfo.CurrentCulture, Strings.RemoveGame_Title_Format, gameName),
            Content = Strings.RemoveGame_Body,
            PrimaryButtonText = Strings.RemoveGame_Keep,
            SecondaryButtonText = Strings.RemoveGame_Delete,
            SecondaryButtonAppearance = ControlAppearance.Danger,
            CloseButtonText = Strings.Common_Cancel,
        };
        MessageBoxResult result = await box.ShowDialogAsync(cancellationToken: ct).ConfigureAwait(true);
        return result switch
        {
            MessageBoxResult.Primary => RemoveGameChoice.KeepSessions,
            MessageBoxResult.Secondary => RemoveGameChoice.DeleteSessions,
            _ => RemoveGameChoice.Cancel,
        };
    }

    /// <summary>beta.11 (D40): the one destructive button, in the danger colour; closing the box or Cancel deletes nothing.</summary>
    [SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
    public async Task<bool> DeleteSessionsAsync(string? gameName, long sessions, CancellationToken ct = default)
    {
        var box = new MessageBox
        {
            Title = gameName is null
                ? Strings.DeleteSessions_AllTitle
                : string.Format(CultureInfo.CurrentCulture, Strings.DeleteSessions_GameTitle_Format, gameName),
            Content = string.Format(CultureInfo.CurrentCulture, gameName is null ? Strings.DeleteSessions_AllBody_Format : Strings.DeleteSessions_GameBody_Format, sessions),
            PrimaryButtonText = Strings.DeleteSessions_Delete,
            PrimaryButtonAppearance = ControlAppearance.Danger,
            CloseButtonText = Strings.Common_Cancel,
        };
        return await box.ShowDialogAsync(cancellationToken: ct).ConfigureAwait(true) == MessageBoxResult.Primary;
    }
}

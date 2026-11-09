// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;

namespace FrameLedger.App.Services;

/// <summary>
/// Every message box the App shows is owned by its window (beta.35). WPF UI 4.3.0's <see cref="Wpf.Ui.Controls.MessageBox"/>
/// sets no owner (measured 2026-11-09: null before and after it is shown), and an ownerless modal window can fall behind the
/// window it was opened from and, when it closes, hand the focus to whichever program Windows activates next. Owned by the
/// App's active window, it opens over it and gives the focus back to it, where the control that had the keyboard takes it
/// again. Every box goes through here (<c>FocusAndMotionTests.EveryMessageBoxGoesThroughMessageBoxes</c>).
/// </summary>
/// <remarks>
/// The App's other dialogs need nothing of the kind: a <c>ContentDialog</c> lives inside the window, and WPF UI's host stores
/// the focused element when it opens and focuses it again when it closes (<c>ContentDialogHostController</c>,
/// <c>FocusAndMotionTests.AContentDialogGivesTheFocusBackToTheControlThatOpenedIt</c>).
/// </remarks>
public static class MessageBoxes
{
    /// <summary>Shows <paramref name="box"/> as a dialog owned by the App's active window.</summary>
    public static async Task<Wpf.Ui.Controls.MessageBoxResult> ShowAsync(Wpf.Ui.Controls.MessageBox box, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(box);
        Own(box);
        return await box.ShowDialogAsync(cancellationToken: ct).ConfigureAwait(true);
    }

    /// <summary>The App's active window, else its main one while it is shown, becomes <paramref name="box"/>'s owner.</summary>
    internal static void Own(Window box)
    {
        ArgumentNullException.ThrowIfNull(box);
        System.Windows.Application? app = System.Windows.Application.Current;
        Window? owner = app?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible && !ReferenceEquals(w, box))
                        ?? (app?.MainWindow is { IsVisible: true } main && !ReferenceEquals(main, box) ? main : null);
        if (owner is not null)
        {
            box.Owner = owner;
        }
    }
}

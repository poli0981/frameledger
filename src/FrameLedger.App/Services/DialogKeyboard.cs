using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>
/// <c>08_UI</c> §Accessibility: Esc closes the open dialog with no result — the same as its Close or Cancel button.
/// WPF UI 4.3.0 handles no key in <c>ContentDialog</c> or <c>MessageBox</c> (both sources at the 4.3.0 tag, read
/// 2026-09-15), so one class handler on <see cref="Window"/> does it for every window this app opens: a
/// <c>MessageBox</c> closes itself; any other window hides the <c>ContentDialog</c> its <see cref="ContentDialogHost"/>
/// is showing.
/// </summary>
/// <remarks>
/// The BUBBLING key event, not the preview: a control that uses Esc itself — a dropdown closing its list, a text box
/// with an IME composition — handles it first, and only an Esc nobody wanted reaches the window. Esc is never an
/// acceptance: the consent dialog and the Legal Gate already treat a close without a result as the refusal.
/// </remarks>
public static class DialogKeyboard
{
    private static int _registered;

    /// <summary>Once per process, before the first window (<c>App.OnStartup</c>).</summary>
    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        EventManager.RegisterClassHandler(typeof(Window), UIElement.KeyDownEvent, new KeyEventHandler(OnKeyDown));
    }

    /// <summary>
    /// Hides the dialog <paramref name="host"/> shows, with no result; false when it shows none, or when the dialog offers
    /// no Close button — a dialog built to be answered is not dismissed by a key it never offered.
    /// </summary>
    public static bool CloseOpenDialog(ContentDialogHost? host)
    {
        if (host?.Content is not ContentDialog dialog || string.IsNullOrEmpty(dialog.CloseButtonText))
        {
            return false;
        }

        dialog.Hide(ContentDialogResult.None);
        return true;
    }

    /// <summary>The message box's own Close button, pressed: the path its template takes, so the result is the one a click gives.</summary>
    public static bool CloseMessageBox(Wpf.Ui.Controls.MessageBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        if (string.IsNullOrEmpty(box.CloseButtonText) || !box.IsCloseButtonEnabled)
        {
            return false;
        }

        box.TemplateButtonCommand.Execute(Wpf.Ui.Controls.MessageBoxButton.Close);
        return true;
    }

    private static void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.Handled)
        {
            return;
        }

        if (sender is Wpf.Ui.Controls.MessageBox box)
        {
            e.Handled = CloseMessageBox(box);
            return;
        }

        if (sender is Window window && CloseOpenDialog(ContentDialogHost.GetForWindow(window) ?? FindHost(window)))
        {
            e.Handled = true;
        }
    }

    /// <summary>The host by walking the visual tree, for a window whose host has not registered itself yet.</summary>
    private static ContentDialogHost? FindHost(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is ContentDialogHost host)
            {
                return host;
            }

            if (FindHost(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}

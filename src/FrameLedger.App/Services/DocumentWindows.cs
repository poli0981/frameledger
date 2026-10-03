// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using FrameLedger.App.Windows;

namespace FrameLedger.App.Services;

/// <summary>
/// <see cref="IDocumentWindows"/> over <see cref="DocumentWindow"/> and <see cref="AboutWindow"/>: one window of each kind —
/// asking again brings the open one forward rather than stacking copies — owned by the shell.
/// </summary>
public sealed class DocumentWindows(IUrlOpener urls, IThemeApplier theme, AppearanceSettings appearance, ShellHost shell) : IDocumentWindows
{
    private DocumentWindow? _guide;
    private DocumentWindow? _limitations;
    private AboutWindow? _about;

    public void ShowGuide()
    {
        if (Reveal(_guide))
        {
            return;
        }

        _guide = new DocumentWindow(Strings.Guide_Title, EmbeddedDocuments.Guide(), urls, theme, appearance) { Owner = shell.Current };
        _guide.Closed += (_, _) => _guide = null;
        _guide.Show();
    }

    public void ShowLimitations()
    {
        if (Reveal(_limitations))
        {
            return;
        }

        _limitations = new DocumentWindow(Strings.Limitations_Title, [new DocumentPage(Strings.Limitations_Title, LimitationsDocument.Load(), LimitationsDocument.ResourceName)], urls, theme, appearance)
        {
            Owner = shell.Current,
        };
        _limitations.Closed += (_, _) => _limitations = null;
        _limitations.Show();
    }

    public void ShowAbout()
    {
        if (Reveal(_about))
        {
            return;
        }

        _about = new AboutWindow(urls, theme, appearance) { Owner = shell.Current };
        _about.Closed += (_, _) => _about = null;
        _about.Show();
    }

    private static bool Reveal(Window? open)
    {
        if (open is null)
        {
            return false;
        }

        if (open.WindowState == WindowState.Minimized)
        {
            open.WindowState = WindowState.Normal;
        }

        _ = open.Activate();
        return true;
    }
}

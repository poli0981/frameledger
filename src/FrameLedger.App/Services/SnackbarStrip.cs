// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>
/// <see cref="IMessageStrip"/> over the shell's <see cref="ISnackbarService"/> — and, since beta.35 (D89), said to a screen
/// reader as it is shown: a message at the bottom of the window that closes after four seconds was never heard. Every
/// transient message goes through here (<c>AccessibilityTests.OnlyTheStripShowsASnackbar</c>).
/// </summary>
/// <remarks>
/// Every message is drawn in the theme's colours (beta.35), its icon saying which kind it is. WPF UI 4.3.0's Success and
/// Caution appearances are fixed colours under white text, 2.78:1 and 2.16:1 in both themes, and no Windows contrast theme
/// reaches them; its Secondary one is the theme's, 13.97:1 dark and 16.53:1 light (measured 2026-11-09).
/// </remarks>
public sealed class SnackbarStrip : IMessageStrip
{
    private static readonly TimeSpan _duration = TimeSpan.FromSeconds(4);
    private readonly ISnackbarService _snackbar;
    private readonly IAnnouncer _announcer;

    public SnackbarStrip(ISnackbarService snackbar, IAnnouncer? announcer = null)
    {
        _snackbar = snackbar ?? throw new ArgumentNullException(nameof(snackbar));
        _announcer = announcer ?? NoAnnouncer.Instance;
    }

    public void Info(string title, string body) => Show(title, body, SymbolRegular.Info24, AnnouncementKind.Status);

    public void Success(string title, string body) => Show(title, body, SymbolRegular.Checkmark24, AnnouncementKind.Status);

    public void Warn(string title, string body) => Show(title, body, SymbolRegular.Warning24, AnnouncementKind.Alert);

    private void Show(string title, string body, SymbolRegular icon, AnnouncementKind kind)
    {
        _snackbar.Show(title, body, ControlAppearance.Secondary, new SymbolIcon(icon), _duration);
        _announcer.Announce(SpokenText.TitleAndBody(title, body), kind, "Strip");
    }
}

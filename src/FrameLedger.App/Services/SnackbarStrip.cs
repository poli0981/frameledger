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

    public void Info(string title, string body) => Show(title, body, ControlAppearance.Secondary, SymbolRegular.Info24, AnnouncementKind.Status);

    public void Success(string title, string body) => Show(title, body, ControlAppearance.Success, SymbolRegular.Checkmark24, AnnouncementKind.Status);

    public void Warn(string title, string body) => Show(title, body, ControlAppearance.Caution, SymbolRegular.Warning24, AnnouncementKind.Alert);

    private void Show(string title, string body, ControlAppearance appearance, SymbolRegular icon, AnnouncementKind kind)
    {
        _snackbar.Show(title, body, appearance, new SymbolIcon(icon), _duration);
        _announcer.Announce(SpokenText.TitleAndBody(title, body), kind, "Strip");
    }
}

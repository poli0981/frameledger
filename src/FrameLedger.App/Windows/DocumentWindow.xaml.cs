// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using FrameLedger.App.Controls;
using FrameLedger.App.Services;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Windows;

/// <summary>
/// Embedded documents in a window of their own (beta.12): Help ▸ Limitations — until now a message box holding the raw
/// Markdown in a text box — and the user guide. The theme is applied before the XAML like every secondary window's.
/// </summary>
public partial class DocumentWindow : FluentWindow
{
    private readonly IUrlOpener _urls;

    public DocumentWindow(string title, IReadOnlyList<DocumentPage> pages, IUrlOpener urls, IThemeApplier theme, AppearanceSettings appearance)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(appearance);
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
        theme.Apply(appearance.Theme, null);
        InitializeComponent();
        Title = title;
        TitleBarControl.Title = title;
        Browser.UrlOpener = urls;
        Browser.Pages = pages;
    }

    /// <summary>The pages and the one shown, for the host and the tests.</summary>
    public DocumentBrowser Pages => Browser;

    private void OnOpenOnline(object sender, RoutedEventArgs e)
    {
        if (Browser.SelectedPage is { } page)
        {
            _ = _urls.Open(page.Url);
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}

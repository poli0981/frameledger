// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using FrameLedger.App.Services;

namespace FrameLedger.App.Controls;

/// <summary>
/// Pages of embedded documents beside one rendered page (beta.12): Help ▸ Limitations, About's two document tabs, the
/// user guide. A link from one page to another page it holds opens that page here (and scrolls to the heading it names);
/// a link to any other file of the repository opens on GitHub at this build's source; the rest is
/// <see cref="MarkdownView"/>'s.
/// </summary>
public partial class DocumentBrowser : UserControl
{
    private IReadOnlyList<DocumentPage> _pages = [];

    public DocumentBrowser()
    {
        InitializeComponent();
        View.RepositoryLinkRequested += OnRepositoryLink;
    }

    /// <summary>Raised when another page is shown.</summary>
    public event EventHandler? PageChanged;

    /// <summary>The pages, in the order the list shows them.</summary>
    public IReadOnlyList<DocumentPage> Pages
    {
        get => _pages;
        set
        {
            _pages = value ?? [];
            PageList.ItemsSource = _pages;
            PageList.Visibility = _pages.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            PageList.SelectedIndex = _pages.Count > 0 ? 0 : -1;
            if (_pages.Count == 0)
            {
                ShowPage(null);
            }
        }
    }

    /// <summary>The page shown.</summary>
    public DocumentPage? SelectedPage => PageList.SelectedItem as DocumentPage;

    /// <summary>How a link leaves the App.</summary>
    public IUrlOpener UrlOpener
    {
        get => View.UrlOpener;
        set => View.UrlOpener = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>The viewer, for a test's look at what it drew.</summary>
    internal MarkdownView Viewer => View;

    /// <summary>Shows the page at <paramref name="path"/> and scrolls to <paramref name="anchor"/>; whether this browser holds that page.</summary>
    public bool Show(string path, string? anchor = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        DocumentPage? page = _pages.FirstOrDefault(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase));
        if (page is null)
        {
            return false;
        }

        if (ReferenceEquals(page, SelectedPage))
        {
            if (anchor is { Length: > 0 })
            {
                _ = View.ScrollTo(anchor);
            }

            return true;
        }

        if (anchor is { Length: > 0 })
        {
            // After the new page's first layout: before it there is nothing to bring into view.
            void ScrollOnce(object? s, EventArgs e)
            {
                View.LayoutUpdated -= ScrollOnce;
                _ = View.ScrollTo(anchor);
            }

            View.LayoutUpdated += ScrollOnce;
        }

        PageList.SelectedItem = page;
        return true;
    }

    private void OnPageSelected(object sender, SelectionChangedEventArgs e) => ShowPage(SelectedPage);

    private void ShowPage(DocumentPage? page)
    {
        View.DocumentPath = page?.Path;
        View.IsPlainText = page?.IsPlainText ?? false;
        View.MarkdownText = page?.Text ?? string.Empty;
        AutomationProperties.SetName(View, page?.Title ?? string.Empty);
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnRepositoryLink(object? sender, RepositoryLinkEventArgs e) => e.Handled = Show(e.Path, e.Anchor);
}

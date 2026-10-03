// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.Windows;

namespace FrameLedger.App.Tests;

/// <summary>
/// Help ▸ About and Help ▸ Limitations as windows of rendered documents (beta.12): About carries the program's legal
/// notices and opens the licence and NOTICE on its legal tab; its two document tabs hold every page they name; a page's
/// link to another page it holds opens that page rather than GitHub; Limitations is one page, with no page list.
/// </summary>
public sealed class DocumentWindowsTests
{
    private sealed class NoTheme : IThemeApplier
    {
        public void Apply(AppTheme theme, Window? window)
        {
        }
    }

    [Fact]
    public async Task AboutShowsTheLegalNoticesAndOpensTheLicenceAndNoticeOnItsLegalTab()
    {
        var urls = new NoUrlOpener();
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            var about = new AboutWindow(urls, new NoTheme(), new AppearanceSettings(new MemorySettings()));
            try
            {
                string[] legal = [.. about.LegalPages.Pages.Select(static p => p.Path)];
                string[] thirdParty = [.. about.ThirdPartyPages.Pages.Select(static p => p.Path)];
                about.ShowLegal("NOTICE");
                (int Tab, string? Page, bool Plain) notice = (about.TabView.SelectedIndex, about.LegalPages.SelectedPage?.Path, about.LegalPages.SelectedPage?.IsPlainText ?? false);
                about.ShowLegal("LICENSE");
                string? licence = about.LegalPages.SelectedPage?.Path;
                bool linked = about.ThirdPartyPages.Show(EmbeddedDocuments.PackageIndex);
                return new { legal, thirdParty, notice, licence, linked, Index = about.ThirdPartyPages.SelectedPage?.Path };
            }
            finally
            {
                about.Close();
            }
        });

        facts.legal.Should().Equal("legal/EULA.md", "LICENSE", "legal/DISCLAIMER.md", "legal/PRIVACY_POLICY.md", "NOTICE", "legal/TRADEMARKS.md");
        facts.thirdParty.Should().Equal(EmbeddedDocuments.ThirdPartyNotices, EmbeddedDocuments.PackageIndex);
        facts.notice.Should().Be((AboutWindow.LegalTab, "NOTICE", true), "NOTICE is shown as written");
        facts.licence.Should().Be("LICENSE");
        facts.linked.Should().BeTrue();
        facts.Index.Should().Be(EmbeddedDocuments.PackageIndex);
        urls.Opened.Should().BeEmpty("nothing left the App");
        AboutWindow.SourceCode.Should().Be(new Uri("https://github.com/poli0981/frameledger/tree/main"));
    }

    /// <summary>beta.12 (D44): Help ▸ User guide — the guide's pages with a list, Limitations last, a section link opening its page.</summary>
    [Fact]
    public async Task TheGuideListsItsPagesAndOpensAnotherPageAtItsSection()
    {
        var urls = new NoUrlOpener();
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            var window = new DocumentWindow(Strings.Guide_Title, EmbeddedDocuments.Guide(), urls, new NoTheme(), new AppearanceSettings(new MemorySettings()));
            try
            {
                string? first = window.Pages.SelectedPage?.Path;
                bool shown = window.Pages.Show("guide/01-install.md", "updates");
                return new
                {
                    Pages = window.Pages.Pages.Count,
                    First = first,
                    Shown = shown,
                    Now = window.Pages.SelectedPage?.Path,
                    ListShown = window.Pages.FindName("PageList") is UIElement { Visibility: Visibility.Visible },
                    Unknown = window.Pages.Show("docs/03_METRICS.md"),
                };
            }
            finally
            {
                window.Close();
            }
        });

        facts.Pages.Should().Be(EmbeddedDocuments.GuidePages.Count + 1);
        facts.First.Should().Be("guide/README.md");
        facts.Shown.Should().BeTrue();
        facts.Now.Should().Be("guide/01-install.md");
        facts.ListShown.Should().BeTrue();
        facts.Unknown.Should().BeFalse("a file the window does not hold opens on GitHub instead");
        urls.Opened.Should().BeEmpty();
    }

    [Fact]
    public async Task LimitationsIsOnePageWithNoListAndOpensOnGitHubAtThisBuildsSource()
    {
        var urls = new NoUrlOpener();
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            var page = new DocumentPage(Strings.Limitations_Title, LimitationsDocument.Load(), LimitationsDocument.ResourceName);
            var window = new DocumentWindow(Strings.Limitations_Title, [page], urls, new NoTheme(), new AppearanceSettings(new MemorySettings()));
            try
            {
                return new
                {
                    window.Title,
                    Pages = window.Pages.Pages.Count,
                    Selected = window.Pages.SelectedPage?.Path,
                    ListShown = window.Pages.FindName("PageList") is UIElement { Visibility: Visibility.Visible },
                    Anchors = window.Pages.Viewer.Anchors.Count,
                };
            }
            finally
            {
                window.Close();
            }
        });

        facts.Title.Should().Be(Strings.Limitations_Title);
        facts.Pages.Should().Be(1);
        facts.Selected.Should().Be("LIMITATIONS.md");
        facts.ListShown.Should().BeFalse("one page needs no list");
        facts.Anchors.Should().BeGreaterThan(3, "every section of LIMITATIONS is a heading a link can scroll to");
        LimitationsDocument.OnGitHub.Should().Be(new Uri("https://github.com/poli0981/frameledger/blob/main/LIMITATIONS.md"));
    }
}

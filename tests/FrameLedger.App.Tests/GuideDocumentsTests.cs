// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using FrameLedger.App.Markdown;
using FrameLedger.App.Services;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace FrameLedger.App.Tests;

/// <summary>
/// The user guide (beta.12, D44): every page the App lists is embedded and titled by its first heading, every page in
/// <c>guide/</c> is listed, and every link in it leads somewhere — another guide page or Limitations (opened in the
/// window), a file that exists in the repository (opened on GitHub at this build's source), or an https page. A guide
/// with a dead link tells a new user something false about where to look.
/// </summary>
public sealed class GuideDocumentsTests
{
    private static string RepoRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "FrameLedger.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("the repository root (FrameLedger.slnx) is not above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void EveryPageIsEmbeddedTitledAndListedAndLimitationsClosesTheList()
    {
        IReadOnlyList<DocumentPage> pages = EmbeddedDocuments.Guide();

        pages.Should().HaveCount(EmbeddedDocuments.GuidePages.Count + 1);
        pages[0].Title.Should().Be("FrameLedger user guide");
        pages.Take(EmbeddedDocuments.GuidePages.Count).Should().OnlyContain(static p => p.Text.Length > 300 && !p.Title.EndsWith(".md", StringComparison.Ordinal));
        pages[^1].Path.Should().Be(LimitationsDocument.ResourceName);

        string[] onDisk = [.. Directory.EnumerateFiles(Path.Combine(RepoRoot(), "guide"), "*.md").Select(static f => "guide/" + Path.GetFileName(f)).Order(StringComparer.Ordinal)];
        EmbeddedDocuments.GuidePages.Order(StringComparer.Ordinal).Should().Equal(onDisk, "a page in guide/ that the App does not list could never be read in the App");
    }

    [Fact]
    public void EveryLinkInTheGuideLeadsSomewhere()
    {
        string root = RepoRoot();
        HashSet<string> inWindow = [.. EmbeddedDocuments.Guide().Select(static p => p.Path)];
        var dead = new List<string>();
        foreach (string page in EmbeddedDocuments.GuidePages)
        {
            MarkdownDocument ast = Markdig.Markdown.Parse(EmbeddedDocuments.Read(page), MarkdownRenderer.Pipeline);
            foreach (string href in ast.Descendants<LinkInline>().Where(static l => !l.IsImage).Select(static l => l.Url ?? string.Empty)
                         .Concat(ast.Descendants<AutolinkInline>().Select(static a => a.IsEmail ? "mailto:" + a.Url : a.Url)))
            {
                MarkdownLinkTarget target = MarkdownLinks.Resolve(page, href);
                bool leads = target switch
                {
                    { External: { } url } => url.Scheme is "https" or "mailto",
                    { RepositoryPath: { } path } => inWindow.Contains(path) || File.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))),
                    { Anchor: { } } => true,
                    _ => false,
                };
                if (!leads)
                {
                    dead.Add($"{page}: {href}");
                }
            }
        }

        dead.Should().BeEmpty();
    }

    /// <summary>
    /// Every link to a section — on the same page, another guide page, Limitations, or a document in the repository — names an
    /// anchor the App's own renderer gives that page's headings (beta.17: the guide grew from eight pages to twelve, and its
    /// pages link into each other's sections). Rendered as the window renders them, so GitHub's rules for a repeated heading
    /// and for punctuation are the ones checked.
    /// </summary>
    [Fact]
    public async Task EveryLinkToASectionNamesAHeadingOfItsPage()
    {
        string root = RepoRoot();
        var wanted = new List<(string Page, string Target, string Anchor)>();
        foreach (string page in EmbeddedDocuments.GuidePages)
        {
            MarkdownDocument ast = Markdig.Markdown.Parse(EmbeddedDocuments.Read(page), MarkdownRenderer.Pipeline);
            foreach (string href in ast.Descendants<LinkInline>().Where(static l => !l.IsImage).Select(static l => l.Url ?? string.Empty))
            {
                MarkdownLinkTarget target = MarkdownLinks.Resolve(page, href);
                if (target is { Anchor: { } anchor, External: null })
                {
                    wanted.Add((page, target.RepositoryPath ?? page, anchor));
                }
            }
        }

        wanted.Should().NotBeEmpty("the guide links into its own sections");
        List<string> dead = await PagesLoadTests.OnStaAsync(() =>
        {
            var missing = new List<string>();
            var anchors = new Dictionary<string, IReadOnlyDictionary<string, System.Windows.Documents.Block>>(StringComparer.Ordinal);
            foreach ((string page, string target, string anchor) in wanted)
            {
                if (!anchors.TryGetValue(target, out IReadOnlyDictionary<string, System.Windows.Documents.Block>? known))
                {
                    bool limitations = string.Equals(target, LimitationsDocument.ResourceName, StringComparison.Ordinal);
                    string text = limitations ? LimitationsDocument.Load()
                        : target.StartsWith("guide/", StringComparison.Ordinal) ? EmbeddedDocuments.Read(target)
                        : File.ReadAllText(Path.Combine(root, target.Replace('/', Path.DirectorySeparatorChar)));
                    known = MarkdownRenderer.Render(text, target).Anchors;
                    anchors[target] = known;
                }

                if (!known.ContainsKey(anchor))
                {
                    missing.Add($"{page}: {target}#{anchor}");
                }
            }

            return missing;
        });

        string.Join(Environment.NewLine, dead).Should().BeEmpty("a section link that names no heading opens the page at its top, or nowhere");
    }

    /// <summary>The guide's contents list names its pages in the order the App lists them (beta.17).</summary>
    [Fact]
    public void TheContentsListThePagesInTheAppsOrder()
    {
        MarkdownDocument ast = Markdig.Markdown.Parse(EmbeddedDocuments.Read("guide/README.md"), MarkdownRenderer.Pipeline);
        string[] listed = [.. ast.Descendants<LinkInline>()
            .Select(static l => MarkdownLinks.Resolve("guide/README.md", l.Url).RepositoryPath)
            .OfType<string>()
            .Where(static path => path.StartsWith("guide/", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)];

        listed.Should().Equal(EmbeddedDocuments.GuidePages.Skip(1), "guide/README.md is the contents, and the window lists the pages in that order");
    }

    /// <summary>
    /// "Every setting" names every setting the Settings page shows, by its English label (beta.17): a setting added to the
    /// page and not to the guide is red here, not a question a player cannot find the answer to.
    /// </summary>
    [Fact]
    public void TheSettingsPageOfTheGuideNamesEverySetting()
    {
        string xaml = File.ReadAllText(Path.Combine(RepoRoot(), "src", "FrameLedger.App", "Pages", "SettingsPage.xaml"));
        IEnumerable<string> keys = Regex.Matches(xaml, @"res:Strings\.(?<key>Settings_\w+_Label)\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1))
            .Select(static m => m.Groups["key"].Value)
            .Concat(["Settings_Theme_Label", "Settings_Language_Label"]);

        Unnamed(keys, EmbeddedDocuments.Read("guide/08-settings.md")).Should().BeEmpty("guide/08-settings.md names every setting the page shows");
    }

    /// <summary>"The screens" names every page of the navigation and every menu item, by its English text (beta.17).</summary>
    [Fact]
    public void TheScreensPageOfTheGuideNamesEveryPageAndMenuItem()
    {
        string xaml = File.ReadAllText(Path.Combine(RepoRoot(), "src", "FrameLedger.App", "MainWindow.xaml"));
        IEnumerable<string> keys = Regex.Matches(xaml, @"res:Strings\.(?<key>(?:Nav|Menu)_\w+)\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1))
            .Select(static m => m.Groups["key"].Value);

        Unnamed(keys, EmbeddedDocuments.Read("guide/07-screens.md")).Should().BeEmpty("guide/07-screens.md names every page and menu item");
    }

    /// <summary>
    /// The English text of each key that <paramref name="page"/> does not contain — the menu's access-key underscore and a
    /// trailing ellipsis removed, since the guide writes "File ▸ Add game".
    /// </summary>
    private static List<string> Unnamed(IEnumerable<string> keys, string page) =>
    [
        .. keys.Distinct(StringComparer.Ordinal)
            .Select(static key => (Key: key, Text: (Strings.ResourceManager.GetString(key, CultureInfo.GetCultureInfo("en")) ?? key).Replace("_", string.Empty, StringComparison.Ordinal).TrimEnd('…', '.')))
            .Where(k => !page.Contains(k.Text, StringComparison.Ordinal))
            .Select(static k => $"{k.Key}: \"{k.Text}\""),
    ];
}

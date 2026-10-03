// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
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

    [Fact]
    public void ALinkToAnotherSectionOfTheGuideNamesAHeadingThatExists()
    {
        // 05-your-data.md links to 01-install.md#updates: the anchor must be one the target page's headings produce.
        string install = EmbeddedDocuments.Read("guide/01-install.md");
        MarkdownDocument ast = Markdig.Markdown.Parse(install, MarkdownRenderer.Pipeline);
        string[] anchors = [.. ast.Descendants<HeadingBlock>().Select(static h => MarkdownLinks.Slug(string.Concat(h.Inline!.Descendants<LiteralInline>().Select(static l => l.Content.ToString()))))];

        anchors.Should().Contain("updates");
    }
}

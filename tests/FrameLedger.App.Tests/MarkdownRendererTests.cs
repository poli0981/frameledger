// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using FluentAssertions;
using FrameLedger.App.Markdown;
using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>
/// The documents the App shows, rendered (beta.12, owner request 4): what each Markdown construct becomes, that HTML is
/// text and images are never fetched, that a link the App will not follow keeps its address in view — and that NOTHING
/// any embedded document says is lost on the way: every word of its plain text is in the rendered document.
/// </summary>
public sealed partial class MarkdownRendererTests
{
    private const string _sample = """
        # Title

        Some **bold**, *italic*, ~~struck~~ and `code` text with a [link](docs/03_METRICS.md#the-games-memory).

        ## The game's memory

        - one
        - two

        3. third
        4. fourth

        > quoted

        | Name | Value |
        |:-----|------:|
        | a    | 1     |

        ```
        line one
          line two
        ```

        - [x] done
        - [ ] open

        <b>not bold</b> and ![a chart](https://example.invalid/chart.png) and [run](javascript:alert(1)).

        ---
        """;

    private static IEnumerable<T> All<T>(FlowDocument document)
        where T : DependencyObject => Descend(document).OfType<T>();

    private static IEnumerable<DependencyObject> Descend(DependencyObject node) =>
        new[] { node }.Concat(LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().SelectMany(Descend));

    private static string TextOf(FlowDocument document) => new TextRange(document.ContentStart, document.ContentEnd).Text;

    [Fact]
    public async Task EachConstructBecomesItsFlowElement()
    {
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            RenderedMarkdown r = MarkdownRenderer.Render(_sample, "README.md");
            FlowDocument d = r.Document;
            Paragraph[] paragraphs = [.. All<Paragraph>(d)];
            Paragraph title = paragraphs.First(p => TextOfInlines(p).StartsWith("Title", StringComparison.Ordinal));
            List[] lists = [.. All<List>(d)];
            Table table = All<Table>(d).Single();
            Hyperlink[] links = [.. All<Hyperlink>(d)];
            return new
            {
                TitleSize = title.FontSize,
                Anchors = r.Anchors.Keys.ToArray(),
                Lists = lists.Select(static l => (l.MarkerStyle, l.ListItems.Count, l.StartIndex)).ToArray(),
                TableRows = table.RowGroups[0].Rows.Count,
                HeaderWeight = table.RowGroups[0].Rows[0].FontWeight,
                Quote = All<Section>(d).Any(),
                Struck = All<Span>(d).Any(static s => s.TextDecorations == TextDecorations.Strikethrough),
                Links = links.Select(static l => l.NavigateUri?.OriginalString).ToArray(),
                Text = TextOf(d),
                Code = paragraphs.Any(static p => p.FontFamily.Source.Contains("Consolas", StringComparison.Ordinal) && TextOfInlines(p).Contains("line one", StringComparison.Ordinal)),
                Images = Descend(d).OfType<System.Windows.Controls.Image>().Count(),
            };
        });

        facts.TitleSize.Should().BeGreaterThan(MarkdownRenderer.BodySize);
        facts.Anchors.Should().Equal("title", "the-games-memory");
        facts.Lists.Should().HaveCount(3);
        facts.Lists[0].Should().Be((TextMarkerStyle.Disc, 2, 1));
        facts.Lists[1].Should().Be((TextMarkerStyle.Decimal, 2, 3), "an ordered list keeps the number it starts at");
        facts.TableRows.Should().Be(2);
        facts.HeaderWeight.Should().Be(FontWeights.SemiBold);
        facts.Quote.Should().BeTrue();
        facts.Struck.Should().BeTrue();
        facts.Code.Should().BeTrue("a fenced block is monospace and keeps its lines");
        facts.Text.Should().Contain("  line two", "a code block keeps its indentation");
        facts.Links.Should().Equal(["docs/03_METRICS.md#the-games-memory"], "the image is not a link and javascript: is not followed");
        facts.Text.Should().Contain("<b>not bold</b>", "HTML is shown as the text it is, never interpreted");
        facts.Text.Should().Contain("[a chart]", "an image's description stands in for it");
        facts.Images.Should().Be(0, "no image is fetched");
        facts.Text.Should().Contain("run (javascript:alert(1))", "a link the App will not follow keeps its address in view");
        facts.Text.Should().Contain("☑ done").And.Contain("☐ open");
    }

    [Fact]
    public async Task APlainTextKeepsEveryLineAsWritten()
    {
        string text = await PagesLoadTests.OnStaAsync(() => TextOf(MarkdownRenderer.RenderPlainText("  0. Definitions.\r\n\r\n  # not a heading\r\n").Document));

        text.Should().Contain("  0. Definitions.").And.Contain("  # not a heading", "the GPL is not Markdown: its numbered sections are not a list");
    }

    /// <summary>Every embedded document — the Legal Gate's four, LIMITATIONS, NOTICE, the trademark note, the third-party notices, the package index and the user guide's twelve pages (eight until beta.17).</summary>
    [Fact]
    public async Task NothingAnyEmbeddedDocumentSaysIsDropped()
    {
        IReadOnlyList<(string Path, string Text, bool Plain)> documents =
        [
            .. LegalDocuments.Load().Select(static d => (d.Path, d.Text, d.IsPlainText)),
            ("LIMITATIONS.md", LimitationsDocument.Load(), false),
            (EmbeddedDocuments.Notice, EmbeddedDocuments.Read(EmbeddedDocuments.Notice), true),
            (EmbeddedDocuments.Trademarks, EmbeddedDocuments.Read(EmbeddedDocuments.Trademarks), false),
            (EmbeddedDocuments.ThirdPartyNotices, EmbeddedDocuments.Read(EmbeddedDocuments.ThirdPartyNotices), false),
            (EmbeddedDocuments.PackageIndex, EmbeddedDocuments.Read(EmbeddedDocuments.PackageIndex), false),
            .. EmbeddedDocuments.GuidePages.Select(static p => (p, EmbeddedDocuments.Read(p), false)),
        ];

        List<string> missing = await PagesLoadTests.OnStaAsync(() =>
        {
            var problems = new List<string>();
            foreach ((string path, string text, bool plain) in documents)
            {
                string rendered = TextOf((plain ? MarkdownRenderer.RenderPlainText(text) : MarkdownRenderer.Render(text, path)).Document);
                // Markdig's plain text writes a task's box as "[x]"; the renderer draws it as ☑, which is the same fact.
                string expected = plain ? text : TaskBox().Replace(Markdig.Markdown.ToPlainText(text, MarkdownRenderer.Pipeline), " ");
                // A table Markdig did not take as a table shows its delimiter row as text ("|---|---|") — every word kept, the
                // table lost. GitHub renders such a table; the App must too.
                if (!plain && TableSource().IsMatch(rendered))
                {
                    problems.Add($"{path}: a table is shown as its Markdown source: '{TableSource().Match(rendered).Value}'");
                }

                Dictionary<string, int> have = Count(rendered);
                foreach ((string word, int count) in Count(expected))
                {
                    if (have.GetValueOrDefault(word) < count)
                    {
                        problems.Add($"{path}: '{word}' {count}× in the text, {have.GetValueOrDefault(word)}× rendered");
                    }
                }
            }

            return problems;
        });

        documents.Should().HaveCount(21, "nine documents and the guide's twelve pages");
        missing.Should().BeEmpty("the renderer must never shorten a document a user reads or accepts");
    }

    [Fact]
    public async Task ARepeatedHeadingGetsGitHubsNumberedAnchor()
    {
        string[] anchors = await PagesLoadTests.OnStaAsync(() => MarkdownRenderer.Render("## Notes\n\ntext\n\n## Notes\n").Anchors.Keys.ToArray());

        anchors.Should().Equal("notes", "notes-1");
    }

    private static string TextOfInlines(Paragraph p) => string.Concat(p.Inlines.Select(MarkdownRenderer.TextOf));

    private static Dictionary<string, int> Count(string text)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Match m in Word().Matches(text))
        {
            counts[m.Value] = counts.GetValueOrDefault(m.Value) + 1;
        }

        return counts;
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Word();

    [GeneratedRegex(@"\[[xX ]\]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TaskBox();

    [GeneratedRegex(@"\|\s*:?-{3,}:?\s*\|", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TableSource();
}

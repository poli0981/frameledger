// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdBlock = Markdig.Syntax.Block;
using MdInline = Markdig.Syntax.Inlines.Inline;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using TableColumnAlign = Markdig.Extensions.Tables.TableColumnAlign;
using WpfBlock = System.Windows.Documents.Block;
using WpfInline = System.Windows.Documents.Inline;
using WpfList = System.Windows.Documents.List;
using WpfTable = System.Windows.Documents.Table;
using WpfTableCell = System.Windows.Documents.TableCell;
using WpfTableRow = System.Windows.Documents.TableRow;

namespace FrameLedger.App.Markdown;

/// <summary>
/// Markdown as a WPF <see cref="FlowDocument"/> (beta.12, owner request 4): the Legal Gate, Help ▸ Limitations, the update
/// notes, the About window and the user guide show their documents rendered rather than as raw text. Markdig parses
/// (CommonMark with pipe tables, strikethrough, autolinks and task lists); <b>HTML is off</b> — a tag in a document is shown
/// as the text it is, never interpreted; <b>no image is fetched</b> — its description stands in; and a node this renderer
/// does not know is shown as its own source text, so the renderer never shortens a document
/// (<c>MarkdownRendererTests</c>: nothing dropped, for every document the App embeds).
/// </summary>
/// <remarks>
/// Colours and the font come from the theme by resource reference, so a document follows a theme change while it is open;
/// WPF-UI 4.3.0 styles no <see cref="FlowDocument"/>, whose own defaults are black Georgia on white.
/// </remarks>
public static class MarkdownRenderer
{
    /// <summary>The body text size, device-independent pixels.</summary>
    public const double BodySize = 14;

    private static readonly double[] _headingSizes = [26, 21, 18, 16, 15, BodySize];

    private static readonly FontFamily _mono = new("Cascadia Mono, Consolas, Courier New");

    /// <summary>One pipeline for every document; also what the tests compare the rendered text against.</summary>
    public static MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .UseAutoLinks()
        .UseTaskLists()
        .DisableHtml()
        .Build();

    /// <summary>The document parsed and drawn, with its headings' anchors.</summary>
    /// <param name="markdown">The text.</param>
    /// <param name="documentPath">Its path from the repository root, which its relative links are resolved against.</param>
    public static RenderedMarkdown Render(string markdown, string? documentPath = null)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        MarkdownDocument ast = Markdig.Markdown.Parse(markdown, Pipeline);
        var context = new MarkdownRenderContext(markdown, documentPath);
        FlowDocument document = NewDocument();
        foreach (MdBlock block in ast)
        {
            AddBlock(document.Blocks, block, context);
        }

        return new RenderedMarkdown(document, context.Anchors);
    }

    /// <summary>A text that is not Markdown — the GPL's own <c>LICENSE</c> — as it is written: monospace, every line kept.</summary>
    public static RenderedMarkdown RenderPlainText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        FlowDocument document = NewDocument();
        var paragraph = new Paragraph { FontFamily = _mono, FontSize = BodySize - 1 };
        AddLines(paragraph.Inlines, text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'));
        document.Blocks.Add(paragraph);
        return new RenderedMarkdown(document, new Dictionary<string, WpfBlock>(StringComparer.Ordinal));
    }

    /// <summary>The text a rendered inline shows, for anchors and image descriptions.</summary>
    internal static string TextOf(WpfInline inline) => inline switch
    {
        Run run => run.Text,
        LineBreak => " ",
        Span span => string.Concat(span.Inlines.Select(TextOf)),
        _ => string.Empty,
    };

    private static FlowDocument NewDocument()
    {
        var document = new FlowDocument
        {
            FontSize = BodySize,
            PagePadding = new Thickness(4, 0, 12, 12),
            TextAlignment = TextAlignment.Left,
            Background = Brushes.Transparent,
            IsOptimalParagraphEnabled = false,
            IsHyphenationEnabled = false,
        };
        document.SetResourceReference(TextElement.ForegroundProperty, "TextFillColorPrimaryBrush");
        document.SetResourceReference(TextElement.FontFamilyProperty, "ContentControlThemeFontFamily");
        return document;
    }

    private static void AddBlock(BlockCollection blocks, MdBlock block, MarkdownRenderContext context)
    {
        switch (block)
        {
            case HeadingBlock heading:
                blocks.Add(HeadingOf(heading, context));
                break;
            case ParagraphBlock paragraph:
                blocks.Add(ParagraphOf(paragraph, context));
                break;
            case QuoteBlock quote:
                blocks.Add(QuoteOf(quote, context));
                break;
            case ListBlock list:
                blocks.Add(ListOf(list, context));
                break;
            case MdTable table:
                blocks.Add(TableOf(table, context));
                break;
            case CodeBlock code:   // fenced or indented
                blocks.Add(CodeOf(code));
                break;
            case ThematicBreakBlock:
                blocks.Add(RuleOf());
                break;
            case LinkReferenceDefinitionGroup:
                break;   // "[label]: url" lines define links; they are not text
            case ContainerBlock container:
                foreach (MdBlock child in container)
                {
                    AddBlock(blocks, child, context);
                }

                break;
            default:
                // A block this renderer has no drawing for, as the text it was written as — shown, never dropped.
                blocks.Add(new Paragraph(new Run(context.Source(block.Span))) { Margin = new Thickness(0, 0, 0, 10) });
                break;
        }
    }

    private static Paragraph HeadingOf(HeadingBlock heading, MarkdownRenderContext context)
    {
        int level = Math.Clamp(heading.Level, 1, _headingSizes.Length);
        var paragraph = new Paragraph
        {
            FontSize = _headingSizes[level - 1],
            FontWeight = level <= 2 ? FontWeights.SemiBold : FontWeights.Bold,
            Margin = new Thickness(0, level == 1 ? 4 : 16, 0, 6),
        };
        AddInlines(paragraph.Inlines, heading.Inline, context);
        string anchor = MarkdownLinks.Slug(string.Concat(paragraph.Inlines.Select(TextOf)));
        if (anchor.Length > 0)
        {
            // GitHub numbers a repeated heading's anchor: the second "Notes" is #notes-1.
            string unique = anchor;
            for (int n = 1; context.Anchors.ContainsKey(unique); n++)
            {
                unique = anchor + "-" + n.ToString(CultureInfo.InvariantCulture);
            }

            context.Anchors[unique] = paragraph;
        }

        return paragraph;
    }

    private static Paragraph ParagraphOf(ParagraphBlock block, MarkdownRenderContext context)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 10), LineHeight = BodySize * 1.45 };
        AddInlines(paragraph.Inlines, block.Inline, context);
        return paragraph;
    }

    private static Section QuoteOf(QuoteBlock quote, MarkdownRenderContext context)
    {
        var section = new Section { Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(12, 4, 0, 0), BorderThickness = new Thickness(3, 0, 0, 0) };
        section.SetResourceReference(WpfBlock.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
        section.SetResourceReference(TextElement.ForegroundProperty, "TextFillColorSecondaryBrush");
        foreach (MdBlock child in quote)
        {
            AddBlock(section.Blocks, child, context);
        }

        return section;
    }

    private static WpfList ListOf(ListBlock list, MarkdownRenderContext context)
    {
        var wpf = new WpfList
        {
            MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(24, 0, 0, 0),
        };
        if (list.IsOrdered && int.TryParse(list.OrderedStart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int start) && start > 1)
        {
            wpf.StartIndex = start;
        }

        foreach (MdBlock item in list)
        {
            var listItem = new ListItem();
            AddBlock(listItem.Blocks, item, context);

            // A list item's paragraphs sit closer together than the document's.
            foreach (Paragraph p in listItem.Blocks.OfType<Paragraph>())
            {
                p.Margin = new Thickness(0, 0, 0, 4);
            }

            wpf.ListItems.Add(listItem);
        }

        return wpf;
    }

    private static WpfTable TableOf(MdTable table, MarkdownRenderContext context)
    {
        var wpf = new WpfTable { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 12), BorderThickness = new Thickness(1, 1, 0, 0) };
        wpf.SetResourceReference(WpfBlock.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
        MdTableRow[] rows = [.. table.OfType<MdTableRow>()];
        int columns = Math.Max(table.ColumnDefinitions.Count, rows.Select(static r => r.Count).DefaultIfEmpty(0).Max());
        for (int i = 0; i < columns; i++)
        {
            wpf.Columns.Add(new TableColumn { Width = GridLength.Auto });
        }

        var group = new TableRowGroup();
        foreach (MdTableRow row in rows)
        {
            var wpfRow = new WpfTableRow();
            if (row.IsHeader)
            {
                wpfRow.FontWeight = FontWeights.SemiBold;
                wpfRow.SetResourceReference(TextElement.BackgroundProperty, "SubtleFillColorSecondaryBrush");
            }

            int column = 0;
            foreach (MdTableCell cell in row.OfType<MdTableCell>())
            {
                wpfRow.Cells.Add(CellOf(cell, column < table.ColumnDefinitions.Count ? table.ColumnDefinitions[column].Alignment : null, context));
                column++;
            }

            group.Rows.Add(wpfRow);
        }

        wpf.RowGroups.Add(group);
        return wpf;
    }

    private static WpfTableCell CellOf(MdTableCell cell, TableColumnAlign? alignment, MarkdownRenderContext context)
    {
        var wpfCell = new WpfTableCell
        {
            Padding = new Thickness(8, 4, 8, 4),
            BorderThickness = new Thickness(0, 0, 1, 1),
            TextAlignment = alignment switch
            {
                TableColumnAlign.Center => TextAlignment.Center,
                TableColumnAlign.Right => TextAlignment.Right,
                _ => TextAlignment.Left,
            },
        };
        wpfCell.SetResourceReference(WpfTableCell.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
        foreach (MdBlock child in cell)
        {
            AddBlock(wpfCell.Blocks, child, context);
        }

        foreach (Paragraph p in wpfCell.Blocks.OfType<Paragraph>())
        {
            p.Margin = new Thickness(0);
        }

        return wpfCell;
    }

    private static Paragraph CodeOf(CodeBlock code)
    {
        var paragraph = new Paragraph { FontFamily = _mono, FontSize = BodySize - 1, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 10) };
        paragraph.SetResourceReference(TextElement.BackgroundProperty, "SubtleFillColorSecondaryBrush");
        AddLines(paragraph.Inlines, [.. code.Lines.Lines.Take(code.Lines.Count).Select(static l => l.Slice.ToString())]);
        return paragraph;
    }

    private static Paragraph RuleOf()
    {
        var rule = new Paragraph { Margin = new Thickness(0, 6, 0, 12), BorderThickness = new Thickness(0, 0, 0, 1), FontSize = 2 };
        rule.SetResourceReference(WpfBlock.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
        return rule;
    }

    private static void AddLines(InlineCollection inlines, string[] lines)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                inlines.Add(new LineBreak());
            }

            inlines.Add(new Run(lines[i]));
        }
    }

    private static void AddInlines(InlineCollection inlines, ContainerInline? container, MarkdownRenderContext context)
    {
        if (container is not null)
        {
            inlines.AddRange(ChildrenOf(container, context));
        }
    }

    private static List<WpfInline> ChildrenOf(ContainerInline container, MarkdownRenderContext context)
    {
        var children = new List<WpfInline>();
        foreach (MdInline child in container)
        {
            children.Add(InlineOf(child, context));
        }

        return children;
    }

    private static WpfInline InlineOf(MdInline inline, MarkdownRenderContext context) => inline switch
    {
        LiteralInline literal => new Run(literal.Content.ToString()),
        LineBreakInline lineBreak => lineBreak.IsHard ? new LineBreak() : new Run(" "),
        CodeInline code => CodeSpanOf(code.Content),
        HtmlEntityInline entity => new Run(entity.Transcoded.ToString()),
        TaskList task => new Run(task.Checked ? "☑" : "☐"),   // the text after the box keeps its own space
        AutolinkInline autolink => LinkOf(autolink.IsEmail ? "mailto:" + autolink.Url : autolink.Url, [new Run(autolink.Url)], context),
        LinkInline { IsImage: true } image => ImageDescriptionOf(image, context),
        LinkInline link => LinkOf(link.Url, ChildrenOf(link, context), context),
        EmphasisInline emphasis => EmphasisOf(emphasis, context),
        ContainerInline container => SpanOf(ChildrenOf(container, context)),

        // An inline this renderer has no drawing for, as the text it was written as.
        _ => new Run(context.Source(inline.Span)),
    };

    private static Span SpanOf(IEnumerable<WpfInline> children)
    {
        var span = new Span();
        span.Inlines.AddRange(children);
        return span;
    }

    private static Span EmphasisOf(EmphasisInline emphasis, MarkdownRenderContext context)
    {
        Span span = emphasis.DelimiterChar == '~'
            ? new Span { TextDecorations = TextDecorations.Strikethrough }
            : emphasis.DelimiterCount >= 2 ? new Bold() : new Italic();
        AddInlines(span.Inlines, emphasis, context);
        return span;
    }

    private static Run CodeSpanOf(string text)
    {
        var run = new Run(text) { FontFamily = _mono, FontSize = BodySize - 1 };
        run.SetResourceReference(TextElement.BackgroundProperty, "SubtleFillColorSecondaryBrush");
        return run;
    }

    /// <summary>
    /// A link the viewer can follow (<see cref="MarkdownLinks"/> decides where to). One the App refuses to follow is its text
    /// alone, with the address beside it, so nothing the author wrote is lost and nothing it names is opened.
    /// </summary>
    private static WpfInline LinkOf(string? url, IReadOnlyList<WpfInline> label, MarkdownRenderContext context)
    {
        if (MarkdownLinks.Resolve(context.DocumentPath, url).IsRefused || !Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out Uri? uri))
        {
            Span text = SpanOf(label);
            if (!string.IsNullOrWhiteSpace(url))
            {
                text.Inlines.Add(new Run(" (" + url + ")"));
            }

            return text;
        }

        var hyperlink = new Hyperlink { NavigateUri = uri, ToolTip = url };
        hyperlink.Inlines.AddRange(label);
        hyperlink.SetResourceReference(TextElement.ForegroundProperty, "AccentTextFillColorPrimaryBrush");
        return hyperlink;
    }

    /// <summary>An image is never fetched — a document must not reach the network to be read — so its description stands in.</summary>
    private static Run ImageDescriptionOf(LinkInline image, MarkdownRenderContext context)
    {
        string description = string.Concat(ChildrenOf(image, context).Select(TextOf)).Trim();
        var run = new Run("[" + (description.Length > 0 ? description : image.Url ?? string.Empty) + "]") { FontStyle = FontStyles.Italic };
        run.SetResourceReference(TextElement.ForegroundProperty, "TextFillColorSecondaryBrush");
        return run;
    }
}

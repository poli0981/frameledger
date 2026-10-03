// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using Markdig.Syntax;
using WpfBlock = System.Windows.Documents.Block;

namespace FrameLedger.App.Markdown;

/// <summary>One render's state: the source text a node can fall back to, the document's place in the repository, the anchors so far.</summary>
internal sealed class MarkdownRenderContext(string source, string? documentPath)
{
    public string? DocumentPath { get; } = documentPath;

    public Dictionary<string, WpfBlock> Anchors { get; } = new(StringComparer.Ordinal);

    /// <summary>The text a node was written as; empty for a span outside the source (a node the parser made up).</summary>
    public string Source(SourceSpan span) =>
        span.IsEmpty || span.Start < 0 || span.End >= source.Length ? string.Empty : source.Substring(span.Start, span.Length);
}

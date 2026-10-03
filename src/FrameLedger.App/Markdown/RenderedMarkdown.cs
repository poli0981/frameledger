// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows.Documents;

namespace FrameLedger.App.Markdown;

/// <summary>
/// A document as <see cref="MarkdownRenderer"/> drew it: the <see cref="FlowDocument"/> a viewer shows, and each heading's
/// block by its GitHub-style anchor (<c>#the-games-memory</c>), so an in-document link scrolls to its section.
/// </summary>
public sealed record RenderedMarkdown(FlowDocument Document, IReadOnlyDictionary<string, Block> Anchors);

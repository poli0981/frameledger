// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Markdown;

/// <summary>
/// Where a link in a shown document leads (<see cref="MarkdownLinks.Resolve"/>): out to the browser (<see cref="External"/>),
/// to a section of the same document (<see cref="Anchor"/>), to a file of the repository (<see cref="RepositoryPath"/>,
/// shown in the App when it embeds that file, on GitHub at this build's source otherwise) — or nowhere, for a link the App
/// refuses to follow (<see cref="IsRefused"/>: a scheme other than http, https or mailto, or a path that leaves the
/// repository).
/// </summary>
public sealed record MarkdownLinkTarget
{
    public Uri? External { get; init; }

    /// <summary>The anchor without its <c>#</c>, lower-case; on its own for a link inside the document, or with <see cref="RepositoryPath"/>.</summary>
    public string? Anchor { get; init; }

    /// <summary>A path from the repository root with forward slashes, never leaving it (<c>docs/03_METRICS.md</c>).</summary>
    public string? RepositoryPath { get; init; }

    public bool IsRefused => External is null && Anchor is null && RepositoryPath is null;

    public static MarkdownLinkTarget Refused { get; } = new();
}

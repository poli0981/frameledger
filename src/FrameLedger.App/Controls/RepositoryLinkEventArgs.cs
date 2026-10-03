// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Controls;

/// <summary>
/// A link in a shown document to another file of the repository (<see cref="MarkdownView.RepositoryLinkRequested"/>): the
/// host sets <see cref="Handled"/> when it shows that file itself; otherwise it opens on GitHub at this build's source.
/// </summary>
public sealed class RepositoryLinkEventArgs(string path, string? anchor) : EventArgs
{
    /// <summary>From the repository root, forward slashes.</summary>
    public string Path { get; } = path;

    /// <summary>The heading anchor the link names, without its <c>#</c>; null for none.</summary>
    public string? Anchor { get; } = anchor;

    public bool Handled { get; set; }
}

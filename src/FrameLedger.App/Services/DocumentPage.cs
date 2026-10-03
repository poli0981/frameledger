// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>
/// One page a document window shows (beta.12): its title, its text as this build embeds it, its path in the repository
/// (which its relative links resolve against, and where "Open on GitHub" goes), and whether it is a plain text shown as
/// written rather than Markdown.
/// </summary>
public sealed record DocumentPage(string Title, string Text, string Path, bool IsPlainText = false)
{
    /// <summary>The same file on GitHub, at this build's source.</summary>
    public Uri Url => RepositoryLinks.Blob(Path);
}

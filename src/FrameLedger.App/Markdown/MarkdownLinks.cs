// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Text;

namespace FrameLedger.App.Markdown;

/// <summary>
/// Resolves the links of a document the App shows (beta.12): http, https and mailto go to the browser; <c>#section</c>
/// scrolls within the document; a relative path is resolved against the document's own place in the repository, the way
/// GitHub resolves it, and stays inside the repository. Every other scheme — <c>file:</c>, <c>javascript:</c>, a UNC path —
/// is refused: a document is text to read, and nothing in it may start a program or open a local file.
/// </summary>
public static class MarkdownLinks
{
    /// <summary>Where <paramref name="href"/>, found in the document at <paramref name="documentPath"/>, leads.</summary>
    /// <param name="documentPath">The document's path from the repository root (<c>legal/EULA.md</c>); null for a text with no place in it.</param>
    /// <param name="href">The link as written.</param>
    public static MarkdownLinkTarget Resolve(string? documentPath, string? href)
    {
        string link = (href ?? string.Empty).Trim();
        if (link.Length == 0)
        {
            return MarkdownLinkTarget.Refused;
        }

        if (link[0] == '#')
        {
            return link.Length > 1 ? new MarkdownLinkTarget { Anchor = NormaliseAnchor(link[1..]) } : MarkdownLinkTarget.Refused;
        }

        if (Uri.TryCreate(link, UriKind.Absolute, out Uri? absolute) && !link.StartsWith('/'))
        {
            return absolute.Scheme is "http" or "https" or "mailto" ? new MarkdownLinkTarget { External = absolute } : MarkdownLinkTarget.Refused;
        }

        if (link.Contains(':', StringComparison.Ordinal) || link.StartsWith(@"\\", StringComparison.Ordinal) || link.StartsWith("//", StringComparison.Ordinal))
        {
            return MarkdownLinkTarget.Refused;   // a scheme Uri did not parse, a drive letter, a UNC or protocol-relative path
        }

        string? anchor = null;
        int hash = link.IndexOf('#', StringComparison.Ordinal);
        if (hash >= 0)
        {
            anchor = hash + 1 < link.Length ? NormaliseAnchor(link[(hash + 1)..]) : null;
            link = link[..hash];
        }

        int query = link.IndexOf('?', StringComparison.Ordinal);
        if (query >= 0)
        {
            link = link[..query];
        }

        string? path = Combine(documentPath, Uri.UnescapeDataString(link));
        return path is null ? MarkdownLinkTarget.Refused : new MarkdownLinkTarget { RepositoryPath = path, Anchor = anchor };
    }

    /// <summary>
    /// A heading's anchor as GitHub writes it: lower-case, punctuation removed (letters, digits, spaces, hyphens and
    /// underscores kept), each space a hyphen — "## The game's memory" → <c>the-games-memory</c>.
    /// </summary>
    public static string Slug(string heading)
    {
        ArgumentNullException.ThrowIfNull(heading);
        var slug = new StringBuilder(heading.Length);
#pragma warning disable CA1308 // GitHub writes heading anchors LOWER-cased; this matches them, it compares nothing for security
        string lower = heading.Trim().ToLowerInvariant();
#pragma warning restore CA1308
        foreach (char c in lower)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_')
            {
                slug.Append(c);
            }
            else if (c == ' ')
            {
                slug.Append('-');
            }
        }

        return slug.ToString();
    }

#pragma warning disable CA1308 // the same lower-cased form as Slug
    private static string NormaliseAnchor(string anchor) => Uri.UnescapeDataString(anchor).ToLowerInvariant();
#pragma warning restore CA1308

    /// <summary>The target relative to the document's folder (or the root for <c>/path</c>); null when it climbs out of the repository.</summary>
    private static string? Combine(string? documentPath, string target)
    {
        var parts = new List<string>();
        if (!target.StartsWith('/') && documentPath is { Length: > 0 })
        {
            string[] folder = documentPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            parts.AddRange(folder.Take(folder.Length - 1));
        }

        foreach (string part in target.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(part, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(part, "..", StringComparison.Ordinal))
            {
                if (parts.Count == 0)
                {
                    return null;
                }

                parts.RemoveAt(parts.Count - 1);
                continue;
            }

            parts.Add(part);
        }

        return parts.Count == 0 ? null : string.Join('/', parts);
    }
}

// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Reflection;
using System.Text.RegularExpressions;

namespace FrameLedger.App.Services;

/// <summary>
/// Links into the repository at the source THIS build was made from (beta.12): <c>release.yml</c> passes the release's tag
/// (<c>-p:FrameLedgerSourceRef=v0.1.0-beta.12</c>, a rehearsal its commit), which the App csproj stamps as assembly
/// metadata; a local build carries none and links to <c>main</c>. Until beta.12 every link said <c>blob/main</c>, so an
/// installed release opened documents that had moved on since it was built — a different EULA from the one it showed.
/// </summary>
public static partial class RepositoryLinks
{
    /// <summary>The metadata key the App csproj writes.</summary>
    public const string MetadataKey = "FrameLedgerSourceRef";

    /// <summary>The branch a build without a stamped source links to.</summary>
    public const string DefaultRef = "main";

    /// <summary>The tag or commit this build links to; <see cref="DefaultRef"/> when none was stamped, or when what was stamped is not a plain ref.</summary>
    public static string SourceRef { get; } = Validate(typeof(RepositoryLinks).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(static a => string.Equals(a.Key, MetadataKey, StringComparison.Ordinal))?.Value);

    /// <summary>A file of the repository on GitHub, at <see cref="SourceRef"/>.</summary>
    /// <param name="path">From the repository root, forward slashes (<c>docs/19_SAFETY_AND_ANTICHEAT.md</c>).</param>
    /// <param name="anchor">A heading's anchor, without its <c>#</c>.</param>
    public static Uri Blob(string path, string? anchor = null) => At("blob", path, anchor);

    /// <summary>A folder of the repository on GitHub, at <see cref="SourceRef"/>.</summary>
    public static Uri Tree(string path) => At("tree", path, null);

    /// <summary>The stamped value if it is a ref GitHub can take in a URL path (a tag, a branch, a commit), else <see cref="DefaultRef"/>.</summary>
    internal static string Validate(string? stamped) =>
        stamped is { Length: > 0 and <= 100 } && PlainRef().IsMatch(stamped) && !stamped.Contains("..", StringComparison.Ordinal) ? stamped : DefaultRef;

    private static Uri At(string kind, string path, string? anchor)
    {
        ArgumentNullException.ThrowIfNull(path);
        string escaped = string.Join('/', path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
        string url = IssueLink.Repository + "/" + kind + "/" + SourceRef + (escaped.Length > 0 ? "/" + escaped : string.Empty);
        return new Uri(anchor is { Length: > 0 } ? url + "#" + Uri.EscapeDataString(anchor) : url);
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._\-]*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PlainRef();
}

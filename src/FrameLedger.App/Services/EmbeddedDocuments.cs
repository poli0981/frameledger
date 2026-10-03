// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;

namespace FrameLedger.App.Services;

/// <summary>
/// The documents this build embeds beyond FR-11's (beta.12): <c>NOTICE</c> (the copyright line and the GPLv3 section 7
/// additional terms), <c>legal/TRADEMARKS.md</c>, <c>legal/THIRD_PARTY_NOTICES.md</c> and the package-licence index — what
/// Help ▸ About shows, read from the assembly so the App shows exactly the text it was built with.
/// </summary>
public static class EmbeddedDocuments
{
    public const string Notice = "NOTICE";

    public const string Trademarks = "legal/TRADEMARKS.md";

    public const string ThirdPartyNotices = "legal/THIRD_PARTY_NOTICES.md";

    public const string PackageIndex = "legal/licenses/nuget/INDEX.md";

    /// <summary>An embedded document's text, authoring comments removed.</summary>
    /// <exception cref="InvalidOperationException">The build did not embed it.</exception>
    public static string Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using Stream stream = typeof(EmbeddedDocuments).Assembly.GetManifestResourceStream(path)
            ?? throw new InvalidOperationException($"the build did not embed {path}");
        using var reader = new StreamReader(stream);
        return LegalDocuments.StripComments(reader.ReadToEnd());
    }

    /// <summary>One embedded document as a page.</summary>
    public static DocumentPage Page(string title, string path, bool plainText = false) => new(title, Read(path), path, plainText);

    /// <summary>About ▸ Legal documents: the three texts the Legal Gate shows, the GPL as written, <c>NOTICE</c>, the trademark note.</summary>
    public static IReadOnlyList<DocumentPage> Legal() =>
    [
        .. LegalDocuments.Load().Select(static d => new DocumentPage(d.Title, d.Text, d.Path, d.IsPlainText)),
        Page(Strings.About_Doc_Notice, Notice, plainText: true),
        Page(Strings.About_Doc_Trademarks, Trademarks),
    ];

    /// <summary>About ▸ Third-party licences: the notices and the index of every shipped package with its version and licence.</summary>
    public static IReadOnlyList<DocumentPage> ThirdParty() =>
    [
        Page(Strings.About_Doc_ThirdParty, ThirdPartyNotices),
        Page(Strings.About_Doc_Packages, PackageIndex),
    ];
}

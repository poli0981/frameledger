// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using FrameLedger.Shared;

namespace FrameLedger.App.Services;

/// <summary>
/// FR-11's four documents, embedded from <c>legal/*.md</c> and the root <c>LICENSE</c> at build time so the App
/// shows exactly the text this build was reviewed with. <b>The version lives in the document</b> — its
/// <c>**Version:**</c> line — and the App writes it to <c>legal_acceptance.version</c> on Accept; a document
/// without that line is a build error surfaced by <see cref="Load(BuildLine)"/> (and by <c>LegalDocumentsTests</c>), because
/// an unversioned document could never be re-shown. The GPL text carries no version line of its own: it is
/// GPL-3.0-only, and that is its version. *(Since 2026-10-31, D74: on the alpha line the gate shows a fifth, the Alpha
/// notice, accepted with the other three — <see cref="Load(BuildLine)"/>.)*
/// </summary>
public static partial class LegalDocuments
{
    public const string Eula = "eula";
    public const string Gpl = "gpl";
    public const string Disclaimer = "disclaimer";
    public const string Privacy = "privacy";

    /// <summary>The Alpha notice (D74), the gate's fifth document on the alpha line only.</summary>
    public const string Alpha = AlphaNotice.DocumentKey;

    public const string GplVersion = "GPL-3.0-only";

    /// <summary>The main line's four, in the gate's order; the alpha line's gate adds <see cref="Alpha"/> after them.</summary>
    public static IReadOnlyList<string> Keys { get; } = [Eula, Gpl, Disclaimer, Privacy];

    /// <summary>The documents this build's gate shows (<see cref="BuildLine.Current"/>).</summary>
    public static IReadOnlyList<LegalDocument> Load() => Load(BuildLine.Current);

    /// <summary>
    /// The four documents, in the order the gate shows them — and on the alpha line the Alpha notice after them, accepted
    /// with them (D74, 2026-10-31): an alpha is a beta's source on a prerelease .NET, and its user accepts that first.
    /// </summary>
    public static IReadOnlyList<LegalDocument> Load(BuildLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        LegalDocument[] four =
        [
            Read(Eula, Strings.FirstRun_Doc_Eula, "legal/EULA.md", versioned: true),
            Read(Gpl, Strings.FirstRun_Doc_Gpl, "LICENSE", versioned: false),
            Read(Disclaimer, Strings.FirstRun_Doc_Disclaimer, "legal/DISCLAIMER.md", versioned: true),
            Read(Privacy, Strings.FirstRun_Doc_Privacy, "legal/PRIVACY_POLICY.md", versioned: true),
        ];
        return line.IsAlpha ? [.. four, AlphaNoticeDocument(requiresAcceptance: true)] : four;
    }

    /// <summary>
    /// The Alpha notice as a document. About shows it on every line, for information — EULA §2B names it — and only an
    /// alpha's gate asks for it to be accepted.
    /// </summary>
    public static LegalDocument AlphaNoticeDocument(bool requiresAcceptance) =>
        Read(Alpha, Strings.FirstRun_Doc_Alpha, "legal/ALPHA_NOTICE.md", versioned: true) with { RequiresAcceptance = requiresAcceptance };

    /// <summary>The version a document declares (<c>**Version:** 1.0-draft · …</c>), or null when it declares none.</summary>
    public static string? VersionOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Match m = VersionLine().Match(text);
        return m.Success ? m.Groups["v"].Value : null;
    }

    /// <summary>HTML comments are authoring notes (the accuracy block's markers, review reminders), not text a user accepts.</summary>
    public static string StripComments(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return HtmlComment().Replace(text, string.Empty).Trim();
    }

    private static LegalDocument Read(string key, string title, string path, bool versioned)
    {
        Assembly assembly = typeof(LegalDocuments).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(path) ?? throw new InvalidOperationException($"the build did not embed {path}");
        using var reader = new StreamReader(stream);
        string raw = reader.ReadToEnd();
        string version = versioned
            ? VersionOf(raw) ?? throw new InvalidOperationException($"{path} declares no **Version:** line; FR-11 cannot re-show a document that has no version")
            : GplVersion;
        // The GPL is a plain text — Markdown would turn its numbered sections into lists and its indented lines into code.
        // beta.12 (D45): the GPL is the user's licence, shown for information — GPLv3 §9: "You are not required to accept
        // this License in order to receive or run a copy of the Program." Accept covers the other three.
        return new LegalDocument(key, title, version, StripComments(raw), RepositoryLinks.Blob(path), path, IsPlainText: !versioned, RequiresAcceptance: versioned);
    }

    [GeneratedRegex(@"\*\*Version:\*\*\s*(?<v>[^\s·]+)", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionLine();

    [GeneratedRegex(@"<!--.*?-->\r?\n?", RegexOptions.Singleline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlComment();
}

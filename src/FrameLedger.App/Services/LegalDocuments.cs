using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FrameLedger.App.Services;

/// <summary>
/// FR-11's four documents, embedded from <c>legal/*.md</c> and the root <c>LICENSE</c> at build time so the App
/// shows exactly the text this build was reviewed with. <b>The version lives in the document</b> — its
/// <c>**Version:**</c> line — and the App writes it to <c>legal_acceptance.version</c> on Accept; a document
/// without that line is a build error surfaced by <see cref="Load"/> (and by <c>LegalDocumentsTests</c>), because
/// an unversioned document could never be re-shown. The GPL text carries no version line of its own: it is
/// GPL-3.0-only, and that is its version.
/// </summary>
public static partial class LegalDocuments
{
    public const string Eula = "eula";
    public const string Gpl = "gpl";
    public const string Disclaimer = "disclaimer";
    public const string Privacy = "privacy";

    public const string GplVersion = "GPL-3.0-only";

    private const string _repository = "https://github.com/poli0981/frameledger/blob/main/";

    public static IReadOnlyList<string> Keys { get; } = [Eula, Gpl, Disclaimer, Privacy];

    /// <summary>The four documents, in the order the gate shows them.</summary>
    public static IReadOnlyList<LegalDocument> Load() =>
    [
        Read(Eula, Strings.FirstRun_Doc_Eula, "legal/EULA.md", versioned: true),
        Read(Gpl, Strings.FirstRun_Doc_Gpl, "LICENSE", versioned: false),
        Read(Disclaimer, Strings.FirstRun_Doc_Disclaimer, "legal/DISCLAIMER.md", versioned: true),
        Read(Privacy, Strings.FirstRun_Doc_Privacy, "legal/PRIVACY_POLICY.md", versioned: true),
    ];

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
        return new LegalDocument(key, title, version, StripComments(raw), new Uri(_repository + path));
    }

    [GeneratedRegex(@"\*\*Version:\*\*\s*(?<v>[^\s·]+)", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionLine();

    [GeneratedRegex(@"<!--.*?-->\r?\n?", RegexOptions.Singleline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlComment();
}

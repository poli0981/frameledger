using System.Globalization;
using System.Text;

namespace FrameLedger.App.Services;

/// <summary>
/// The GitHub side of the bug report (<c>10_LOGGING</c> §Bug report flow steps 4–5, P4 PR-3): the issue form's URL
/// with the two short fields it can carry prefilled, the OS line in the form's own spelling, and the environment
/// summary as Markdown for the clipboard fallback. Pure functions, so the flow's tests can pin the exact URL.
/// </summary>
public static class IssueLink
{
    public const string Repository = "https://github.com/poli0981/frameledger";

    /// <summary>Help ▸ Documentation: the README is the user-facing entry point (`docs/` is developer-facing, README line 126).</summary>
    public static readonly Uri Documentation = new(Repository + "#readme");

    /// <summary>
    /// <c>issues/new</c> with the form's field ids (<c>.github/ISSUE_TEMPLATE/bug_report.yml</c>: <c>app-version</c>,
    /// <c>os</c> — hyphenated ids, not the underscores <c>10_LOGGING</c> line 54 spelled) prefilled. GitHub URLs cannot
    /// carry logs; the zip is dragged in by hand.
    /// </summary>
    public static Uri NewIssue(string appVersion, string os)
    {
        ArgumentNullException.ThrowIfNull(appVersion);
        ArgumentNullException.ThrowIfNull(os);
        return new Uri(Repository + "/issues/new?template=bug_report.yml&title=" + Uri.EscapeDataString("[Bug] ")
            + "&labels=bug&app-version=" + Uri.EscapeDataString(appVersion) + "&os=" + Uri.EscapeDataString(os));
    }

    /// <summary>The form's spelling (<c>Windows 11 26100.2314</c>): the product name from the build number, then build and revision.</summary>
    public static string OsText(Version version)
    {
        ArgumentNullException.ThrowIfNull(version);
        string product = version.Major >= 10 && version.Build >= 22000 ? "Windows 11" : version.Major >= 10 ? "Windows 10" : "Windows " + version.Major.ToString(CultureInfo.InvariantCulture);
        string build = version.Build.ToString(CultureInfo.InvariantCulture);
        return version.Revision > 0 ? $"{product} {build}.{version.Revision.ToString(CultureInfo.InvariantCulture)}" : $"{product} {build}";
    }

    /// <summary>The environment summary as a Markdown table — the same keys <c>sysinfo.json</c> carries, for a paste into the issue.</summary>
    public static string Markdown(IReadOnlyDictionary<string, string> sysinfo)
    {
        ArgumentNullException.ThrowIfNull(sysinfo);
        var sb = new StringBuilder();
        sb.Append("| Key | Value |\n|---|---|\n");
        foreach ((string key, string value) in sysinfo)
        {
            sb.Append("| `").Append(key).Append("` | ").Append(value.Replace("|", "\\|", StringComparison.Ordinal)).Append(" |\n");
        }

        return sb.ToString();
    }
}

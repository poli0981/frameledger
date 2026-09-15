using System.Text;
using System.Text.RegularExpressions;

namespace FrameLedger.App.Services;

/// <summary>
/// The "redacted copies" of <c>10_LOGGING</c> §Bug report flow step 2, and what makes <c>legal/PRIVACY_POLICY.md</c> §3's
/// "logs are redacted (user directory paths removed) before bundling" true: the name in every <c>X:\Users\&lt;name&gt;</c>
/// path becomes <c>&lt;user&gt;</c>, and so does the last segment of this user's own profile directory when it lives
/// somewhere else. Applied to the bundle's copies only; the files in <c>logs\</c> keep their paths.
/// </summary>
/// <remarks>
/// Every spelling a log carries: backslashes, the doubled backslashes of Serilog's JSON properties, forward slashes, any
/// case, a name with spaces, and a name the Overlay's ANSI header could only spell with <c>?</c>. Files are read as bytes
/// through Latin-1, which maps each byte to one character and back, so a UTF-8 log (Serilog) and an ANSI one (the Overlay's
/// image path) both come out byte for byte except the names. Over-redaction is the failure direction chosen: another
/// account's folder, or <c>C:\Users\Public</c>, reads <c>&lt;user&gt;</c> too.
/// </remarks>
public sealed partial class LogRedactor
{
    /// <summary>What a user name in a path becomes.</summary>
    public const string Placeholder = "<user>";

    private const string _separator = @"(?:\\{1,2}|/)";
    private const string _end = @"(?=[\\/""'<>|\r\n\t]|\z)";

    private readonly Regex? _profile;

    /// <summary>A redactor for <paramref name="profileDirectory"/>'s owner; null or empty redacts <c>X:\Users\&lt;name&gt;</c> paths only.</summary>
    public LogRedactor(string? profileDirectory)
    {
        _profile = ProfilePattern(profileDirectory);
    }

    /// <summary>The redactor for the user running FrameLedger.</summary>
    public static LogRedactor ForCurrentUser() => new(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>The text with every user name in a path replaced by <see cref="Placeholder"/>.</summary>
    public string Redact(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string result = UsersPath().Replace(text, "${prefix}" + Placeholder);
        return _profile is null ? result : _profile.Replace(result, "${prefix}" + Placeholder);
    }

    /// <summary>A log file's bytes, redacted, and otherwise byte for byte what was read.</summary>
    public byte[] Redact(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Encoding.Latin1.GetBytes(Redact(Encoding.Latin1.GetString(bytes)));
    }

    [GeneratedRegex(@"(?<prefix>\b[A-Za-z]:" + _separator + "Users" + _separator + @")(?<name>[^\\/""'<>|\r\n\t]+?)" + _end, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex UsersPath();

    // A profile outside X:\Users (a redirected or a domain-managed one): its own parent, then its name. The name is matched
    // as its UTF-8 bytes read through Latin-1, which is how Redact(byte[]) sees a Serilog log.
    private static Regex? ProfilePattern(string? profileDirectory)
    {
        if (string.IsNullOrWhiteSpace(profileDirectory))
        {
            return null;
        }

        string[] segments = profileDirectory.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || segments[0].Length != 2 || segments[0][1] != ':'
            || (segments.Length == 3 && string.Equals(segments[1], "Users", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        static string Escaped(string segment) => Regex.Escape(Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(segment)));
        string parent = string.Join(_separator, segments[..^1].Select(Escaped));
        string pattern = @"(?<prefix>\b" + parent + _separator + ")" + Escaped(segments[^1]) + _end;
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(2));
    }
}

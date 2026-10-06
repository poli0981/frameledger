// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.IO;

namespace FrameLedger.App.Services;

/// <summary>
/// <c>FrameLedger.exe --data-dir &lt;folder&gt;</c> (beta.15, D52): which folder this App runs over, read before anything
/// else touches one — the single-instance claim, the log, the ledger.
/// </summary>
/// <remarks>
/// The folder must exist, and it must NOT be the profile's own: the flag is for a copy, so that a test build never opens
/// the ledger the owner records into (a migration a released build cannot read back is how a ledger is lost — HANDOFF
/// §Traps). The comparison is the Agent's (<c>AgentPaths.IsProfile</c>): full paths, case-insensitive, with a trailing
/// separator and a link on the folder itself resolved.
/// </remarks>
public static class DataFolderArgument
{
    public const string Flag = "--data-dir";

    /// <summary>What the command line asks for; <paramref name="exists"/> answers whether a folder exists (a test's own).</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture")]
    public static DataFolderChoice Read(IReadOnlyList<string> args, string profileDirectory, Func<string, bool>? exists = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileDirectory);
        exists ??= Directory.Exists;

        string? value = null;
        int given = 0;
        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            if (string.Equals(arg, Flag, StringComparison.OrdinalIgnoreCase))
            {
                given++;
                value = i + 1 < args.Count ? args[++i] : null;
            }
            else if (arg.StartsWith(Flag + "=", StringComparison.OrdinalIgnoreCase))
            {
                given++;
                value = arg[(Flag.Length + 1)..];
            }
        }

        if (given == 0)
        {
            return DataFolderChoice.Profile;
        }

        if (given > 1)
        {
            return DataFolderChoice.Refused(Strings.DataDir_Twice);
        }

        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal))
        {
            return DataFolderChoice.Refused(Strings.DataDir_NoFolder);
        }

        string folder;
        try
        {
            folder = Normalise(value);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return DataFolderChoice.Refused(string.Format(CultureInfo.CurrentCulture, Strings.DataDir_Missing_Format, value));
        }

        if (string.Equals(folder, Normalise(profileDirectory), StringComparison.OrdinalIgnoreCase))
        {
            return DataFolderChoice.Refused(string.Format(CultureInfo.CurrentCulture, Strings.DataDir_Profile_Format, folder));
        }

        return exists(folder)
            ? DataFolderChoice.Viewer(folder)
            : DataFolderChoice.Refused(string.Format(CultureInfo.CurrentCulture, Strings.DataDir_Missing_Format, folder));
    }

    /// <summary>The folder's full path, without a trailing separator, and its target when the folder itself is a link.</summary>
    internal static string Normalise(string path)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        try
        {
            FileSystemInfo? target = Directory.ResolveLinkTarget(full, returnFinalTarget: true);
            return target is null ? full : Path.TrimEndingDirectorySeparator(target.FullName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return full;
        }
    }
}

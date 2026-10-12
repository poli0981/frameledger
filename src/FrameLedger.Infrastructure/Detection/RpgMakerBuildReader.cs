// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using FrameLedger.Domain.Detection;

namespace FrameLedger.Infrastructure.Detection;

/// <summary>
/// RPG Maker's build facts from the files on disk (beta.19, <c>05_DETECTION</c> §Engine version — Unity, Godot, RPG Maker):
/// the first <see cref="ScriptHeaderBytes"/> of MV/MZ's core script (<c>js/rmmz_core.js</c>, <c>www/js/rpg_core.js</c>,
/// <c>js/rpg_core.js</c>), or <c>Game.ini</c>'s <c>Library=</c> line and that RGSS library's version resource — beside the
/// executable or up to two folders above it, as <c>RpgMakerTitle</c> looks (a launcher-fronted game keeps its runtime in a
/// subfolder: HELLO, HELLO WORLD!'s is <c>swiftshader\</c>, A Doll's Locket's <c>css\</c>). Nothing else is read.
/// </summary>
public static partial class RpgMakerBuildReader
{
    /// <summary>How much of a core script is read: its opening comment block, with room to spare (the version is on line 2).</summary>
    public const int ScriptHeaderBytes = 4096;

    /// <summary>A <c>Game.ini</c> larger than this is not RPG Maker's (it holds four short lines).</summary>
    public const long MaxIniBytes = 64 * 1024;

    /// <summary>How many folders above the executable's own are looked in.</summary>
    public const int FoldersUp = 2;

    private static readonly (string Relative, string Script)[] _scripts =
    [
        (Path.Combine("js", RpgMakerVersion.MzCoreScript), RpgMakerVersion.MzCoreScript),
        (Path.Combine("www", "js", RpgMakerVersion.MvCoreScript), RpgMakerVersion.MvCoreScript),
        (Path.Combine("js", RpgMakerVersion.MvCoreScript), RpgMakerVersion.MvCoreScript),
    ];

    /// <summary>The facts for an entry at <paramref name="exePath"/>, or null when none of the files is there.</summary>
    /// <param name="exePath">The entry's executable.</param>
    /// <param name="rgssLibrary">An RGSS library the install listing holds, for an install with no readable <c>Game.ini</c>; or null.</param>
    public static RpgMakerBuildFacts? Read(string exePath, string? rgssLibrary = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        string? folder = Path.GetDirectoryName(exePath);
        for (int up = 0; up <= FoldersUp && !string.IsNullOrEmpty(folder); up++, folder = Path.GetDirectoryName(folder))
        {
            foreach ((string relative, string script) in _scripts)
            {
                if (ScriptVersion(Path.Combine(folder, relative), script) is { } version)
                {
                    return new RpgMakerBuildFacts { CoreScript = script, CoreScriptVersion = version };
                }
            }

            if (LibraryOf(Path.Combine(folder, "Game.ini")) is { } library)
            {
                return Rgss(library);
            }
        }

        return rgssLibrary is null ? null : Rgss(rgssLibrary);
    }

    /// <summary>The version a core script's header names for <paramref name="script"/>, or null. Public for the tests.</summary>
    public static string? ScriptVersion(string path, string script)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 1);
            byte[] head = new byte[ScriptHeaderBytes];
            int read = file.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
            foreach (Match m in HeaderLine().Matches(Encoding.UTF8.GetString(head, 0, read)))
            {
                if (string.Equals(m.Groups["script"].Value, script, StringComparison.OrdinalIgnoreCase))
                {
                    return RpgMakerVersion.ScriptVersion(m.Groups["version"].Value);
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The library a <c>Game.ini</c> names (<c>Library=System\RGSS301.dll</c>), as a full path under the ini's folder — or
    /// its bare file name when the value points outside that folder, which is then not read. Null without the line.
    /// </summary>
    public static string? LibraryOf(string iniPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(iniPath);
        try
        {
            var ini = new FileInfo(iniPath);
            if (!ini.Exists || ini.Length > MaxIniBytes || ini.DirectoryName is not { } folder)
            {
                return null;
            }

            foreach (string line in File.ReadLines(iniPath, Encoding.Latin1))
            {
                Match m = LibraryLine().Match(line);
                if (!m.Success)
                {
                    continue;
                }

                string value = m.Groups["value"].Value.Trim();
                string full = Path.GetFullPath(Path.Combine(folder, value));
                return full.StartsWith(Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    ? full
                    : Path.GetFileName(value);
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static RpgMakerBuildFacts Rgss(string library)
    {
        IReadOnlyList<int>? version = null;
        if (Path.IsPathFullyQualified(library))
        {
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(library);
                version = info.FileMajorPart == 0 && info.FileMinorPart == 0 && info.FileBuildPart == 0 && info.FilePrivatePart == 0
                    ? null
                    : [info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // The name still carries the version (RGSS301 is 3.0.1).
            }
        }

        return new RpgMakerBuildFacts { RgssLibrary = Path.GetFileName(library), RgssFileVersion = version };
    }

    [GeneratedRegex(@"^//\s*(?<script>rmmz_core\.js|rpg_core\.js)\s+v(?<version>\d{1,2}\.\d{1,3}\.\d{1,3})\b", RegexOptions.ExplicitCapture | RegexOptions.Multiline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex HeaderLine();

    [GeneratedRegex(@"^\s*Library\s*=\s*(?<value>\S.*)$", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 250)]
    private static partial Regex LibraryLine();
}

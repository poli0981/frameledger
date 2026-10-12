// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Text;
using System.Text.RegularExpressions;
using FrameLedger.Domain.Detection;

namespace FrameLedger.Infrastructure.Detection;

/// <summary>
/// Ren'Py's build facts from the files on disk (beta.20, <c>05_DETECTION</c> §Engine version — Ren'Py): the <c>version</c>
/// line of the engine's <c>renpy/vc_version.py</c>, which Ren'Py keeps beside the game's launcher — read from its first
/// <see cref="VersionFileBytes"/> bytes. Nothing else of the engine's files is read.
/// </summary>
public static partial class RenPyBuildReader
{
    /// <summary>How much of <c>vc_version.py</c> is read: the measured file is 109 bytes.</summary>
    public const int VersionFileBytes = 4096;

    /// <summary>The facts for the launcher <paramref name="exePath"/>, or null when its engine's version file names none.</summary>
    public static RenPyBuildFacts? Read(string exePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        string? folder = Path.GetDirectoryName(exePath);
        return folder is null ? null : ReadVersionFile(Path.Combine(folder, "renpy", "vc_version.py"));
    }

    /// <summary>The <c>version</c> a <c>vc_version.py</c> names, or null. Public for the tests.</summary>
    public static RenPyBuildFacts? ReadVersionFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 1);
            byte[] head = new byte[VersionFileBytes];
            int read = file.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
            Match m = VersionLine().Match(Encoding.UTF8.GetString(head, 0, read));
            return m.Success ? new RenPyBuildFacts { VcVersion = m.Groups["version"].Value } : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary><c>version = '8.5.0.25111603'</c> at the start of a line — never <c>version_name</c>, whose name goes on.</summary>
    [GeneratedRegex(@"^version\s*=\s*['""](?<version>[^'""\r\n]{1,64})['""]", RegexOptions.ExplicitCapture | RegexOptions.Multiline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex VersionLine();
}

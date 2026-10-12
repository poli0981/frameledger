// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using FrameLedger.Domain.Detection;

namespace FrameLedger.Infrastructure.Detection;

/// <summary>
/// Unity's build facts from the files on disk (beta.19, <c>05_DETECTION</c> §Engine version — Unity, Godot, RPG Maker): the
/// player library's version resource and the first <see cref="DataHeaderBytes"/> bytes of the data file
/// <c>globalgamemanagers</c>, whose serialized-file header names the editor version (at byte 20 in a 2019.4 build, 48 in
/// later ones). Nothing else of either file is read; the game's process is never opened.
/// </summary>
public static partial class UnityBuildReader
{
    /// <summary>How much of the data file is read: its header, with room to spare (the version sat at 48 at the latest).</summary>
    public const int DataHeaderBytes = 256;

    /// <summary>The facts in the two files, or null when neither gave any.</summary>
    /// <param name="playerPath"><c>UnityPlayer.dll</c>, or null when the install has none.</param>
    /// <param name="dataFilePath"><c>&lt;Game&gt;_Data\globalgamemanagers</c>, or null.</param>
    public static UnityBuildFacts? Read(string? playerPath, string? dataFilePath)
    {
        string? product = null;
        IReadOnlyList<int>? numeric = null;
        if (playerPath is not null)
        {
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(playerPath);
                product = string.IsNullOrWhiteSpace(info.ProductVersion) ? null : info.ProductVersion.Trim();
                numeric = info.FileMajorPart == 0 && info.FileMinorPart == 0 && info.FileBuildPart == 0 && info.FilePrivatePart == 0
                    ? null
                    : [info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // No version resource read: the data file may still say it.
            }
        }

        string? header = dataFilePath is null ? null : DataHeaderVersion(dataFilePath);
        return product is null && numeric is null && header is null
            ? null
            : new UnityBuildFacts { PlayerProductVersion = product, PlayerFileVersion = numeric, DataHeaderVersion = header };
    }

    /// <summary>The editor version in the first <see cref="DataHeaderBytes"/> of a data file, or null. Public for the tests.</summary>
    public static string? DataHeaderVersion(string dataFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataFilePath);
        try
        {
            using var file = new FileStream(dataFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 1);
            byte[] head = new byte[DataHeaderBytes];
            int read = file.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
            Match m = HeaderVersion().Match(Encoding.Latin1.GetString(head, 0, read));
            return m.Success ? UnityVersion.EditorVersion(m.Value) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"\d{1,4}\.\d{1,2}\.\d{1,3}[abfpx]\d{1,3}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex HeaderVersion();
}

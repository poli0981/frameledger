// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FrameLedger.Application.Persistence;
using Serilog;

namespace FrameLedger.App.Services;

/// <summary>
/// The images users choose for their games (beta.23, owner decision D60), kept in the data folder's <c>covers</c> folder as
/// <c>game-&lt;id&gt;-&lt;unix ms&gt;.png</c> — a copy, re-encoded and no larger than 1200 pixels a side, so the user's own
/// file can move or go — and named in <c>games.cover_path</c> relative to the data folder (<c>covers\…</c>), so a copied data
/// folder still finds them. Each choice is a new name: a replaced image is never written over while it is shown, and the old
/// file goes once the row names the new one.
/// </summary>
/// <remarks>
/// <b>A row's <c>cover_path</c> is followed only when it is one of these names</b>, in the folder itself and never through a
/// junction or a symbolic link. A viewer (D52) opens a copy of a ledger that may have come from anywhere, and a path read
/// from it — rooted, a share (<c>\\host\…</c>, which would make Windows offer this user's credentials to that host), or
/// <c>..</c> out of the folder — is never opened.
/// </remarks>
public sealed class CoverStore
{
    public const string FolderName = "covers";
    private const int _largestSide = 1200;
    private static readonly TimeSpan _leftoverAfter = TimeSpan.FromHours(1);

    private readonly string _dataDirectory;
    private readonly IGameRepository _games;
    private readonly TimeProvider _clock;

    public CoverStore(UiMode mode, IGameRepository games, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(mode);
        _dataDirectory = mode.DataDirectory;
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _clock = clock ?? TimeProvider.System;
    }

    public string Folder => Path.Combine(_dataDirectory, FolderName);

    /// <summary>The full path of the image <paramref name="coverPath"/> names, when it is one of ours and is there; null otherwise.</summary>
    public static string? Resolve(string dataDirectory, string? coverPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        if (string.IsNullOrWhiteSpace(coverPath) || Path.IsPathRooted(coverPath))
        {
            return null;
        }

        string[] parts = coverPath.Split('\\', '/');
        if (parts.Length != 2 || !string.Equals(parts[0], FolderName, StringComparison.OrdinalIgnoreCase) || ParseName(parts[1]) is null)
        {
            return null;
        }

        // Nor through a link, the folder's or the file's: the folder is looked at before anything under it is touched.
        var folder = new DirectoryInfo(Path.GetFullPath(Path.Combine(dataDirectory, FolderName)));
        var file = new FileInfo(Path.GetFullPath(Path.Combine(folder.FullName, parts[1])));
        return string.Equals(file.DirectoryName, folder.FullName, StringComparison.OrdinalIgnoreCase)
            && folder.Exists && !IsLink(folder) && file.Exists && !IsLink(file)
                ? file.FullName
                : null;
    }

    /// <summary>
    /// Whether <paramref name="entry"/> is a link to another place — a junction or a symbolic link, which could lead anywhere,
    /// a share included. Other reparse points (a OneDrive file, a deduplicated one) are the entry itself.
    /// </summary>
    private static bool IsLink(FileSystemInfo entry)
    {
        try
        {
            return entry.LinkTarget is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>The game id an image's file name carries — <c>game-&lt;id&gt;-&lt;unix ms&gt;.png</c> — or null for any other name.</summary>
    internal static long? ParseName(string name)
    {
        if (!name.StartsWith("game-", StringComparison.Ordinal) || !name.EndsWith(".png", StringComparison.Ordinal))
        {
            return null;
        }

        string[] middle = name["game-".Length..^".png".Length].Split('-');
        return middle.Length == 2 && middle.All(static p => p.Length is > 0 and <= 19 && p.All(char.IsAsciiDigit))
            && long.TryParse(middle[0], NumberStyles.None, CultureInfo.InvariantCulture, out long id) && long.TryParse(middle[1], NumberStyles.None, CultureInfo.InvariantCulture, out _)
                ? id
                : null;
    }

    /// <summary>
    /// The picked file, copied as a PNG for <paramref name="gameId"/>; the <c>cover_path</c> that names it, or null when the file
    /// is not an image Windows can read, or the folder cannot be written (it is a link to elsewhere, the disk refused).
    /// </summary>
    public Task<string?> ImportAsync(long gameId, string source, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return Task.Run(() => Import(gameId, source), ct);
    }

    /// <summary>Deletes the image <paramref name="coverPath"/> names, when it is one of ours.</summary>
    public void Delete(string? coverPath)
    {
        if (Resolve(_dataDirectory, coverPath) is not { } full)
        {
            return;
        }

        try
        {
            File.Delete(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Information("art: {File} could not be deleted ({Message}); it goes at the next start", Path.GetFileName(full), ex.Message);
        }
    }

    /// <summary>
    /// The images no row names any more — a game deleted with its sessions, removed by the agent (D55), an image replaced
    /// while a delete failed — and a half-written copy older than an hour, deleted; the count. Only files of our own name in
    /// the folder are touched, and a row removed with its sessions kept still owns its image (adding the game back restores
    /// it). Run on the App's own data folder, never a viewer's.
    /// </summary>
    public async Task<int> PruneAsync(CancellationToken ct = default)
    {
        var folder = new DirectoryInfo(Folder);
        if (!folder.Exists || IsLink(folder))
        {
            return 0;
        }

        int deleted = 0;
        foreach (FileInfo file in folder.EnumerateFiles().ToList())
        {
            ct.ThrowIfCancellationRequested();
            bool leftover = file.Name.EndsWith(".png.tmp", StringComparison.Ordinal) && ParseName(file.Name[..^".tmp".Length]) is not null
                && _clock.GetUtcNow() - new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero) > _leftoverAfter;
            bool orphan = false;
            if (ParseName(file.Name) is long id)
            {
                GameRow? row = await _games.FindByIdAsync(id, ct).ConfigureAwait(false);
                orphan = row is null || !string.Equals(row.CoverPath, FolderName + "\\" + file.Name, StringComparison.OrdinalIgnoreCase);
            }

            if (leftover || orphan)
            {
                try
                {
                    file.Delete();
                    deleted++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Information("art: {File} could not be deleted ({Message})", file.Name, ex.Message);
                }
            }
        }

        return deleted;
    }

    private string? Import(long gameId, string source)
    {
        try
        {
            byte[] png = Reencode(File.ReadAllBytes(source));
            var folder = new DirectoryInfo(Folder);
            folder.Create();
            if (IsLink(folder))
            {
                Log.Warning("art: {Folder} is a link to another place; no image is written there", folder.FullName);
                return null;
            }

            string name = string.Create(CultureInfo.InvariantCulture, $"game-{gameId}-{_clock.GetUtcNow().ToUnixTimeMilliseconds()}.png");
            string target = Path.Combine(folder.FullName, name);
            string temporary = target + ".tmp";
            File.WriteAllBytes(temporary, png);
            File.Move(temporary, target, overwrite: false);
            return FolderName + "\\" + name;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException
                                       or COMException or ArgumentException or InvalidOperationException or OverflowException)
        {
            Log.Information("art: the chosen image could not be used ({Message})", ex.Message);
            return null;
        }
    }

    /// <summary>The image as a PNG no larger than 1200 pixels a side.</summary>
    internal static byte[] Reencode(byte[] bytes)
    {
        BitmapSource image = GameArt.DecodeFile(bytes, 0, 0);
        double scale = Math.Min(1, _largestSide / (double)Math.Max(image.PixelWidth, image.PixelHeight));
        BitmapSource sized = scale < 1 ? new TransformedBitmap(image, new ScaleTransform(scale, scale)) : image;
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(sized));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }
}

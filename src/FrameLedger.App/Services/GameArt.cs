// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FrameLedger.Application.Art;
using FrameLedger.Application.Import;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Play;
using FrameLedger.Application.Settings;
using Serilog;

namespace FrameLedger.App.Services;

/// <summary>
/// A game's artwork for its page (beta.23, owner decision D60): the image the user chose for it (Edit…), else what the app of
/// the store that installed it keeps on this PC — Steam's and GOG GALAXY's (<see cref="IStoreArtCache"/>) — and the colour
/// the page's band takes from it (D61). Which store installed the game is asked of the stores' own records, as Play asks
/// (<see cref="StoreRecords.Owning"/>), and only of those two: an itch or Epic game, whose apps keep no image files, has its
/// own image or none. Nothing is downloaded and nothing of a store's is copied.
/// </summary>
/// <remarks>
/// Everything here runs on the thread pool — the records, the files, the decoding, the colour — and the decoded images are
/// frozen, so the page only binds them. The stores' records are read the first time a page asks and kept two minutes (a
/// Steam library is files on whatever drive it is on); decoded images are kept for the twelve most recent files.
/// </remarks>
public sealed class GameArt
{
    private const int _keep = 12;
    private const long _largestFile = 32L * 1024 * 1024;
    private static readonly TimeSpan _recordsFor = TimeSpan.FromMinutes(2);

    private readonly RegisteredSettings _settings;
    private readonly StoreRecordsReader _stores;
    private readonly Dictionary<string, IStoreArtCache> _caches;
    private readonly string _dataDirectory;
    private readonly TimeProvider _clock;
    private readonly Lock _lock = new();
    private readonly LinkedList<(string Key, BitmapSource Image)> _decoded = new();
    private readonly HashSet<string> _unreadable = new(StringComparer.OrdinalIgnoreCase);
    private (DateTimeOffset At, IReadOnlyList<StoreGame> Records)? _records;

    public GameArt(RegisteredSettings settings, StoreRecordsReader stores, IEnumerable<IStoreArtCache> caches, UiMode mode, TimeProvider? clock = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _stores = stores ?? throw new ArgumentNullException(nameof(stores));
        ArgumentNullException.ThrowIfNull(caches);
        ArgumentNullException.ThrowIfNull(mode);
        _caches = caches.ToDictionary(static c => c.Platform, StringComparer.Ordinal);
        _dataDirectory = mode.DataDirectory;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>The game's artwork, or null when there is none to show or Settings ▸ Games turned it off.</summary>
    public Task<GameArtView?> ForAsync(GameRow game, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        return Task.Run(async () =>
        {
            if (!await _settings.GetBooleanAsync(SettingsRegistry.UiGameArt, ct).ConfigureAwait(false))
            {
                return null;
            }

            bool tint = await _settings.GetBooleanAsync(SettingsRegistry.UiGameArtTint, ct).ConfigureAwait(false);
            IReadOnlyList<StoreGame> records = await RecordsAsync().ConfigureAwait(false);
            return Resolve(game, records, tint);
        }, ct);
    }

    /// <summary>The image at <paramref name="path"/>, decoded no larger than <paramref name="maxHeight"/> — the edit dialog's preview of a picked file.</summary>
    public Task<BitmapSource?> PreviewAsync(string path, int maxHeight, CancellationToken ct = default) =>
        Task.Run(() => Decode(path, maxWidth: 0, maxHeight), ct);

    private GameArtView? Resolve(GameRow game, IReadOnlyList<StoreGame> records, bool tint)
    {
        GameArtFiles files = GameArtFiles.None;
        string? platform = null;
        if (StoreRecords.Owning(game.Fingerprint.ExePath, game.StoreId, records) is { } record && _caches.TryGetValue(record.Platform, out IStoreArtCache? cache))
        {
            files = cache.Find(record.StoreId);
            platform = record.Platform;
        }

        // The cover slot: the user's image, else the store's portrait cover, else its landscape header.
        BitmapSource? storeCover = Decode(files.Cover, 0, 450);
        bool landscape = false;
        if (storeCover is null && Decode(files.Header, 460, 0) is { } header)
        {
            storeCover = header;
            landscape = true;
        }

        BitmapSource? own = CoverStore.Resolve(_dataDirectory, game.CoverPath) is { } mine ? Decode(mine, 0, 900) : null;
        BitmapSource? hero = Decode(files.Hero, 1920, 0);
        BitmapSource? cover = own ?? storeCover;
        if (cover is null && hero is null)
        {
            return null;
        }

        Color? colour = tint ? DominantOf(cover ?? hero!) : null;
        string source = own is not null ? Strings.Art_Source_Yours
            : string.Equals(platform, "gog", StringComparison.Ordinal) ? Strings.Art_Source_Gog
            : Strings.Art_Source_Steam;
        return new GameArtView(cover, own is not null, own is null && landscape, storeCover, hero, colour, source);
    }

    private async Task<IReadOnlyList<StoreGame>> RecordsAsync()
    {
        lock (_lock)
        {
            if (_records is { } cached && _clock.GetUtcNow() - cached.At < _recordsFor)
            {
                return cached.Records;
            }
        }

        // Two pages opening at once may both read; the second answer simply replaces the first.
        IReadOnlyList<StoreGame> read = await _stores.ReadAsync("art", _caches.Keys).ConfigureAwait(false);
        lock (_lock)
        {
            _records = (_clock.GetUtcNow(), read);
        }

        return read;
    }

    /// <summary>
    /// The file decoded and frozen — no wider than <paramref name="maxWidth"/> nor taller than <paramref name="maxHeight"/>
    /// (0: no limit), never enlarged — or null: no file, an empty or outsized one, or one Windows' codecs cannot read (a WebP
    /// without the WebP codec, a damaged file). A file that would not decode is said in the log once.
    /// </summary>
    private BitmapSource? Decode(string? path, int maxWidth, int maxHeight)
    {
        if (path is null)
        {
            return null;
        }

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length == 0 || info.Length > _largestFile)
            {
                return null;
            }

            string key = string.Join('|', info.FullName, info.Length, info.LastWriteTimeUtc.Ticks, maxWidth, maxHeight);
            lock (_lock)
            {
                for (LinkedListNode<(string Key, BitmapSource Image)>? node = _decoded.First; node is not null; node = node.Next)
                {
                    if (string.Equals(node.Value.Key, key, StringComparison.Ordinal))
                    {
                        _decoded.Remove(node);
                        _decoded.AddFirst(node);
                        return node.Value.Image;
                    }
                }
            }

            BitmapSource image = DecodeFile(File.ReadAllBytes(info.FullName), maxWidth, maxHeight);
            lock (_lock)
            {
                _decoded.AddFirst((key, image));
                while (_decoded.Count > _keep)
                {
                    _decoded.RemoveLast();
                }
            }

            return image;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException
                                       or COMException or ArgumentException or InvalidOperationException or OverflowException)
        {
            bool first;
            lock (_lock)
            {
                first = _unreadable.Add(path);
            }

            if (first)
            {
                Log.Information("art: {Path} could not be read as an image ({Message})", path, ex.Message);
            }

            return null;
        }
    }

    /// <summary>Decodes bytes into a frozen image no larger than the limits; read whole first, so no file stays open.</summary>
    internal static BitmapSource DecodeFile(byte[] bytes, int maxWidth, int maxHeight)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        int width;
        int height;
        using (var probe = new MemoryStream(bytes, writable: false))
        {
            BitmapFrame frame = BitmapDecoder.Create(probe, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
            width = frame.PixelWidth;
            height = frame.PixelHeight;
        }

        using var stream = new MemoryStream(bytes, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        if (maxWidth > 0 && width > maxWidth && (maxHeight <= 0 || (double)width / maxWidth >= (double)height / maxHeight))
        {
            image.DecodePixelWidth = maxWidth;
        }
        else if (maxHeight > 0 && height > maxHeight)
        {
            image.DecodePixelHeight = maxHeight;
        }

        image.EndInit();
        image.Freeze();
        return image;
    }

    /// <summary>The image's dominant colour (<see cref="ColorMath.Dominant"/>), read from a 48-pixel-wide copy.</summary>
    internal static Color? DominantOf(BitmapSource image)
    {
        ArgumentNullException.ThrowIfNull(image);
        double scale = Math.Min(1, 48.0 / Math.Max(1, image.PixelWidth));
        BitmapSource small = scale < 1 ? new TransformedBitmap(image, new ScaleTransform(scale, scale)) : image;
        var bgra = new FormatConvertedBitmap(small, PixelFormats.Bgra32, null, 0);
        int stride = bgra.PixelWidth * 4;
        byte[] pixels = new byte[stride * bgra.PixelHeight];
        bgra.CopyPixels(pixels, stride, 0);
        return ColorMath.Dominant(pixels);
    }
}

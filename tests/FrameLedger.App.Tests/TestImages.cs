// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FrameLedger.Application.Art;
using FrameLedger.Application.Import;

namespace FrameLedger.App.Tests;

/// <summary>Images and stores for the artwork checks (beta.23, D60/D61): PNGs drawn here, never a store's real artwork.</summary>
internal static class TestImages
{
    /// <summary>A PNG of <paramref name="width"/> × <paramref name="height"/>, its left part <paramref name="left"/> and the rest <paramref name="right"/>.</summary>
    public static byte[] Png(int width, int height, Color left, Color? right = null, double leftShare = 1)
    {
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color c = x < width * leftShare ? left : right ?? left;
                int i = (y * stride) + (x * 4);
                pixels[i] = c.B;
                pixels[i + 1] = c.G;
                pixels[i + 2] = c.R;
                pixels[i + 3] = c.A;
            }
        }

        BitmapSource image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }

    /// <summary>Writes <paramref name="bytes"/> at <paramref name="path"/>, its folder made first; the path.</summary>
    public static string Write(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>A store whose records are the ones given.</summary>
    internal sealed class Store(string platform, params StoreGame[] games) : IStoreLibrarySource
    {
        public string Platform => platform;

        public int Reads { get; private set; }

        public ValueTask<IReadOnlyList<StoreGame>> ListAsync(CancellationToken ct = default)
        {
            Reads++;
            return ValueTask.FromResult<IReadOnlyList<StoreGame>>(games);
        }
    }

    /// <summary>A store app's artwork folder that answers the files given for one id.</summary>
    internal sealed class ArtCache(string platform, string storeId, GameArtFiles files) : IStoreArtCache
    {
        public string Platform => platform;

        public List<string> Asked { get; } = [];

        public GameArtFiles Find(string id)
        {
            Asked.Add(id);
            return string.Equals(id, storeId, StringComparison.Ordinal) ? files : GameArtFiles.None;
        }
    }
}

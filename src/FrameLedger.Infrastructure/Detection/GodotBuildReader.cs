// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Buffers.Binary;
using System.Diagnostics;
using FrameLedger.Domain.Detection;

namespace FrameLedger.Infrastructure.Detection;

/// <summary>
/// Godot's build facts from the files on disk (beta.19, <c>05_DETECTION</c> §Engine version — Unity, Godot, RPG Maker): the
/// 20-byte header of the data pack — <c>GDPC</c>, the pack format, the engine's MAJOR, MINOR and PATCH, little-endian — from
/// a <c>.pck</c> beside the executable, or from a pack appended to the executable, which ends with the pack's size and the
/// magic again; and the executable's version resource. Nothing else of either file is read.
/// </summary>
public static class GodotBuildReader
{
    /// <summary><c>GDPC</c> as the little-endian <c>uint</c> a pack starts (and an appended pack ends) with.</summary>
    public const uint PackMagic = 0x43504447;

    /// <summary>The header: magic, format, major, minor, patch.</summary>
    public const int HeaderBytes = 20;

    /// <summary>What an appended pack leaves at the end of the executable: its size (a 64-bit integer), then the magic.</summary>
    public const int TrailerBytes = 12;

    /// <summary>The facts for <paramref name="exePath"/>, or null when nothing could be read.</summary>
    /// <param name="exePath">The game's executable.</param>
    /// <param name="packPath">The <c>.pck</c> beside it, or null when there is none.</param>
    public static GodotBuildFacts? Read(string exePath, string? packPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        IReadOnlyList<int>? pack = packPath is null ? null : ReadFile(packPath, static s => ReadHeader(s, 0));
        bool embedded = false;
        if (pack is null)
        {
            pack = ReadFile(exePath, ReadAppended);
            embedded = pack is not null;
        }

        string? productName = null;
        string? productVersion = null;
        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(exePath);
            productName = string.IsNullOrWhiteSpace(info.ProductName) ? null : info.ProductName.Trim();
            productVersion = string.IsNullOrWhiteSpace(info.ProductVersion) ? null : info.ProductVersion.Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // The pack, if read, is the stronger witness anyway.
        }

        return pack is null && productName is null && productVersion is null
            ? null
            : new GodotBuildFacts { PackVersion = pack, PackEmbedded = embedded, ExeProductName = productName, ExeProductVersion = productVersion };
    }

    /// <summary>MAJOR, MINOR, PATCH from a header at <paramref name="offset"/>, or null when there is none. Public for the tests.</summary>
    public static IReadOnlyList<int>? ReadHeader(Stream stream, long offset)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (offset < 0 || offset + HeaderBytes > stream.Length)
        {
            return null;
        }

        Span<byte> header = stackalloc byte[HeaderBytes];
        stream.Position = offset;
        if (stream.ReadAtLeast(header, HeaderBytes, throwOnEndOfStream: false) < HeaderBytes
            || BinaryPrimitives.ReadUInt32LittleEndian(header) != PackMagic)
        {
            return null;
        }

        uint major = BinaryPrimitives.ReadUInt32LittleEndian(header[8..]);
        uint minor = BinaryPrimitives.ReadUInt32LittleEndian(header[12..]);
        uint patch = BinaryPrimitives.ReadUInt32LittleEndian(header[16..]);
        return major > 99 || minor > 99 || patch > 99 ? null : [(int)major, (int)minor, (int)patch];
    }

    /// <summary>The header of a pack appended to an executable, found through the trailer it ends with. Public for the tests.</summary>
    public static IReadOnlyList<int>? ReadAppended(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        long length = stream.Length;
        if (length < TrailerBytes + HeaderBytes)
        {
            return null;
        }

        Span<byte> trailer = stackalloc byte[TrailerBytes];
        stream.Position = length - TrailerBytes;
        if (stream.ReadAtLeast(trailer, TrailerBytes, throwOnEndOfStream: false) < TrailerBytes
            || BinaryPrimitives.ReadUInt32LittleEndian(trailer[8..]) != PackMagic)
        {
            return null;
        }

        long size = BinaryPrimitives.ReadInt64LittleEndian(trailer);
        return size < HeaderBytes || size > length - TrailerBytes ? null : ReadHeader(stream, length - TrailerBytes - size);
    }

    private static IReadOnlyList<int>? ReadFile(string path, Func<Stream, IReadOnlyList<int>?> read)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 1);
            return read(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

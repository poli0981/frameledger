// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Persistence;

namespace FrameLedger.Infrastructure.Blobs;

/// <summary><see cref="SeriesCodec"/> behind the port: <c>deflate-le-v1</c>, the one blob encoding.</summary>
public sealed class DeflateSeriesCodec : ISeriesCodec
{
    public string Tag => SeriesCodec.Tag;

    public byte[] EncodeFloat32(ReadOnlySpan<float> values) => SeriesCodec.EncodeFloat32(values.ToArray());

    public byte[] EncodeUInt16(ReadOnlySpan<ushort> values) => SeriesCodec.EncodeUInt16(values.ToArray());

    public byte[] EncodeUInt32(ReadOnlySpan<uint> values) => SeriesCodec.EncodeUInt32(values.ToArray());

    public byte[] EncodeBytes(ReadOnlySpan<byte> values) => SeriesCodec.EncodeBytes(values.ToArray());
}

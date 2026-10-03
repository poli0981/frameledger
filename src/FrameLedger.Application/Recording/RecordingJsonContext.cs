// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Text.Json.Serialization;

namespace FrameLedger.Application.Recording;

/// <summary>Source-generated JSON for the three JSON columns of <c>sessions</c>; no reflection.</summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(Dictionary<string, string?>))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(NgxDriverWords))]
[JsonSerializable(typeof(FgRefusalDetail))]
[JsonSerializable(typeof(DriverProfileRecord))]
public sealed partial class RecordingJsonContext : JsonSerializerContext
{
}

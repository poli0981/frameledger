// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Text.Json.Serialization;
using FrameLedger.Application.Recording;

namespace FrameLedger.Application.Advanced;

/// <summary>Source-generated JSON for what "Advanced metrics" stores (D102); no reflection. Names are camel case, nulls are left out, and a status is its name.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(List<AdvancedFactDocument>))]
[JsonSerializable(typeof(AdvancedSessionDocument))]
[JsonSerializable(typeof(AdvancedSeriesDocument))]
[JsonSerializable(typeof(AdvancedContextDocument))]
[JsonSerializable(typeof(Dictionary<string, SensorSeriesStats>))]
internal sealed partial class AdvancedJsonContext : JsonSerializerContext
{
}

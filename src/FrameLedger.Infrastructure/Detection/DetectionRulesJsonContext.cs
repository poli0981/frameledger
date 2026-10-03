// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Text.Json.Serialization;

namespace FrameLedger.Infrastructure.Detection;

/// <summary>Source-generated serialisation for the detection rules.</summary>
/// <remarks>
/// Source-generated rather than reflection-based because the app ships
/// ReadyToRun and trimming-hostile reflection would be a surprise at publish
/// time, not at build time.
/// </remarks>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = false)]
[JsonSerializable(typeof(DetectionRulesDto.Rules))]
internal sealed partial class DetectionRulesJsonContext : JsonSerializerContext
{
}

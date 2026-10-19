// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Text.Json;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.Application.Recording;

/// <summary>
/// <c>sessions.fg_mix</c> (schema 0020, beta.24, decision D62): <see cref="FgMix"/> as JSON — every frame-generation
/// state the session held, each with its factor, its own rates and its share of the presenting time.
/// </summary>
/// <param name="Rows">States first, by rising factor, then no frame generation, then the time no state claims.</param>
public sealed record FgMixRecord(IReadOnlyList<FgMixRecordRow> Rows)
{
    /// <summary>The column's text for <paramref name="mix"/>, or null when there is none.</summary>
    public static string? ToJson(FgMix? mix) => mix is null
        ? null
        : JsonSerializer.Serialize(new FgMixRecord([.. mix.Rows.Select(FgMixRecordRow.From)]), RecordingJsonContext.Default.FgMixRecord);

    /// <summary>The column's text back, or null for a null/empty/unparsable column (a writer that predates it, or a hand edit).</summary>
    public static FgMixRecord? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, RecordingJsonContext.Default.FgMixRecord);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

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
    /// <remarks>
    /// <b>Read by today's rule</b> (beta.26): a stored state no window of this build would form — a factor above
    /// <see cref="FgWindow.GenerationCeiling"/>, windows that averaged fewer than <see cref="FgWindow.MinPerBucket"/>
    /// application frames, or since beta.27 a factor between two whole multipliers (Silent Hill 2, 2026-10-20: ×2.52) — is
    /// time no state claims, and joins the not-classified row. beta.24 and beta.25 stored such a
    /// row for a stretch presented through with one token per window (Silent Hill 2, 2026-10-19: ×1336.0,
    /// 0.20 → 267.20 FPS), and the summary listed it and the DLSS line cited it. Every other row is as stored, and the
    /// shares still add up to the whole.
    /// </remarks>
    public static FgMixRecord? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            FgMixRecord? record = JsonSerializer.Deserialize(json, RecordingJsonContext.Default.FgMixRecord);
            return record is null ? null : Readable(record);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static FgMixRecord Readable(FgMixRecord record)
    {
        if (record.Rows is null || !record.Rows.Any(IsUnreadableState))
        {
            return record;
        }

        var kept = new List<FgMixRecordRow>();
        double seconds = 0;
        double share = 0;
        int windows = 0;
        foreach (FgMixRecordRow row in record.Rows)
        {
            if (IsUnreadableState(row) || FgMixRecordRow.KindOf(row.Kind) == FgMixKind.Unclassified)
            {
                seconds += row.Seconds;
                share += row.Share;
                windows += row.Windows;
                continue;
            }

            kept.Add(row);
        }

        kept.Add(new FgMixRecordRow(FgMixRecordRow.Token(FgMixKind.Unclassified), null, null, null, seconds, share, windows));
        return new FgMixRecord(kept);
    }

    private static bool IsUnreadableState(FgMixRecordRow row) =>
        FgMixRecordRow.KindOf(row.Kind) == FgMixKind.Generated
        && (row.Factor is not double factor || factor > FgWindow.GenerationCeiling || !FgWindow.IsNearWholeMultiplier(factor)
            || row.NativeFps is not double native || native * FgSteadyState.WindowSeconds < FgWindow.MinPerBucket);
}

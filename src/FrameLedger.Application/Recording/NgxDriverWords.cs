// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Text.Json;

namespace FrameLedger.Application.Recording;

/// <summary>What <c>sessions.ngx_driver_words</c> stores: the driver's raw words as probed, plus how the probing went.</summary>
public sealed record NgxDriverWords
{
    public required string Outcome { get; init; }

    public string? Sr { get; init; }

    public string? Rr { get; init; }

    public string? Fg { get; init; }

    public uint? Driver { get; init; }

    // beta.8 (2026-09-25): the rest of what NvAPI_NGX_GetNGXOverrideState answers, kept since this date so the summary
    // can say what an override the driver reports actually set. Null on a row written before, and unless answered.

    /// <summary>The scaling ratio the driver reports for super resolution.</summary>
    public float? Ratio { get; init; }

    /// <summary>
    /// The performance mode the driver reports, in NGX's own numbering (0 Performance … 3 Ultra Performance, 5 DLAA —
    /// <c>UpscalerNames.FromDriverReport</c>), filled when an override set it. This said <c>NGX_DLSS_SR_MODE</c>'s numbering
    /// until beta.24: Silent Hill 2 under the NVIDIA App's Ultra Performance (profile value 5) was reported as 3.
    /// </summary>
    public uint? Mode { get; init; }

    /// <summary>The render preset for super resolution / ray reconstruction (1 = A … 15 = O).</summary>
    public uint? Preset { get; init; }

    /// <summary>The frame-generation override's frame count target.</summary>
    public uint? FgCount { get; init; }

    /// <summary>The frame-generation render preset.</summary>
    public uint? FgPreset { get; init; }

    /// <summary>The frame-generation mode.</summary>
    public uint? FgMode { get; init; }

    public int Readings { get; init; }

    public int Answered { get; init; }

    public bool Changed { get; init; }

    public string? Detail { get; init; }

    /// <summary>The column read back; null for NULL and for a value this build cannot read.</summary>
    public static NgxDriverWords? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, RecordingJsonContext.Default.NgxDriverWords);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FrameLedger.App.Controls;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Metrics;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.App.Services;

/// <summary>
/// The session summary's "Frame generation in this session" block (beta.24, owner decision D62; <c>08_UI</c> §Session
/// summary): <c>sessions.fg_mix</c> as rows — each state with its factor, its own Native → Displayed and its share of the
/// presenting time, then the time with no frame generation, then the time no state claims — and the bar those shares draw.
/// Never an average across states (CLAUDE.md rule 6); the stored rows are read, never recomputed.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public static class FgMixPresentation
{
    private static readonly CultureInfo _inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// The block's rows in the stored order; empty when the session stored no mix, or one row only — a session in one state
    /// throughout says so in its readout, and a bar of one colour would add nothing.
    /// </summary>
    public static IReadOnlyList<FgMixRowModel> Rows(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        FgMixRecord? mix = row.Tier == CaptureTier.Hooked ? FgMixRecord.Parse(row.FgMixJson) : null;
        return mix is null || mix.Rows.Count < 2 ? [] : [.. mix.Rows.Select(Row)];
    }

    /// <summary>One stored row in the user's words.</summary>
    public static FgMixRowModel Row(FgMixRecordRow stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        FgMixKind kind = FgMixRecordRow.KindOf(stored.Kind);
        double? factor = kind == FgMixKind.Generated ? stored.Factor : null;
        if (kind == FgMixKind.Generated && factor is null)
        {
            // A state always carries its factor; one that does not is read as what it then is — time no state explains.
            kind = FgMixKind.Unclassified;
        }

        string label = kind switch
        {
            FgMixKind.Generated => string.Format(CultureInfo.CurrentCulture, Strings.FgMix_State_Format, factor),
            FgMixKind.None => Strings.FgMix_None,
            _ => Strings.FgMix_Unclassified,
        };
        string rates = kind switch
        {
            FgMixKind.Generated => string.Format(CultureInfo.CurrentCulture, Strings.FgMix_Rates_Format, Formats.Fps(stored.NativeFps), Formats.Fps(stored.DisplayedFps)),
            FgMixKind.None => string.Format(CultureInfo.CurrentCulture, Strings.FgMix_Rate_Format, Formats.Fps(stored.NativeFps)),
            _ => string.Empty,
        };
        return new FgMixRowModel(label, ShareText(stored.Share), rates, Formats.Duration(stored.Seconds), new FgMixSegment(kind, factor, stored.Share));
    }

    /// <summary>A share (0..1) to two decimals, <c>35.33%</c>; a share too small to show at that precision reads <c>&lt;0.01%</c>, never 0.</summary>
    public static string ShareText(double share) => share switch
    {
        <= 0 => 0d.ToString("0.00", CultureInfo.CurrentCulture) + "%",
        < 0.00005 => "<" + 0.01.ToString("0.00", CultureInfo.CurrentCulture) + "%",
        _ => (share * 100).ToString("0.00", CultureInfo.CurrentCulture) + "%",
    };

    /// <summary>The bar's spoken name: every row with its share, in the table's order.</summary>
    public static string AccessibleName(IReadOnlyList<FgMixRowModel> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return string.Format(CultureInfo.CurrentCulture, Strings.FgMix_Bar_Name_Format, string.Join(", ", rows.Select(static r => r.Label + " " + r.Share)));
    }

    /// <summary>
    /// Why a session that counted frame generation shows no mix: only where one could have been stored — a counted factor,
    /// or a count refused only because the state changed —, so the sentence is about the session, not about refusals the
    /// readout already names. Null otherwise.
    /// </summary>
    public static string? Missing(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        bool couldHaveOne = row.Tier == CaptureTier.Hooked
            && row.FgMode is { Length: > 0 } mode && mode is not ("na" or "none")
            && (row.FgFactor is not null || row.FgRefusal is "non_uniform" or "ambiguous_band");
        return couldHaveOne && FgMixRecord.Parse(row.FgMixJson) is null ? Strings.FgMix_NotStored : null;
    }

    /// <summary>
    /// The CSV header's line (<c>03_METRICS</c> §Export schema), InvariantCulture: each row's kind, factor, share, rates and
    /// seconds; <c>N/A</c> for a session with no stored mix.
    /// </summary>
    public static string ExportLine(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        FgMixRecord? mix = FgMixRecord.Parse(row.FgMixJson);
        return mix is null || mix.Rows.Count == 0
            ? "N/A"
            : string.Join("; ", mix.Rows.Select(static r => r.Kind
                + (r.Factor is double f ? " x" + f.ToString("0.00", _inv) : string.Empty)
                + " " + (r.Share * 100).ToString("0.00", _inv) + "%"
                + (r.NativeFps is double n ? " native " + n.ToString("0.0", _inv) : string.Empty)
                + (r.DisplayedFps is double d ? " displayed " + d.ToString("0.0", _inv) : string.Empty)
                + " " + r.Seconds.ToString("0.0", _inv) + " s"));
    }
}

// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Domain.Metrics;

namespace FrameLedger.Application.Recording;

/// <summary>One row of <see cref="FgMixRecord"/>: <see cref="FgMixRow"/> with its kind as a token.</summary>
/// <param name="Kind"><c>generated</c>, <c>none</c> or <c>unclassified</c>.</param>
/// <param name="Factor">A state's pooled factor; null otherwise.</param>
/// <param name="NativeFps">Application frames per second over the row's windows; null for unclassified time.</param>
/// <param name="DisplayedFps">Displayed frames per second over the row's windows; null for unclassified time.</param>
/// <param name="Seconds">The time the row covers.</param>
/// <param name="Share">That time over the session's presenting time (0..1).</param>
/// <param name="Windows">How many five-second windows the row holds.</param>
public sealed record FgMixRecordRow(string Kind, double? Factor, double? NativeFps, double? DisplayedFps, double Seconds, double Share, int Windows)
{
    /// <summary>The token stored for <paramref name="kind"/>.</summary>
    public static string Token(FgMixKind kind) => kind switch
    {
        FgMixKind.Generated => "generated",
        FgMixKind.None => "none",
        _ => "unclassified",
    };

    /// <summary>A stored token back as its kind; a token this build does not know reads as unclassified — time no state claims.</summary>
    public static FgMixKind KindOf(string? token) => token switch
    {
        "generated" => FgMixKind.Generated,
        "none" => FgMixKind.None,
        _ => FgMixKind.Unclassified,
    };

    public static FgMixRecordRow From(FgMixRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new FgMixRecordRow(Token(row.Kind), row.Factor, row.NativeFps, row.DisplayedFps, row.Seconds, row.Share, row.Windows);
    }
}

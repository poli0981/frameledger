// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FrameLedger.App.Services;

namespace FrameLedger.App.Charts;

/// <summary>
/// FR-6.3 since beta.30 (owner decision D70): one day on which a session's hardware snapshot differs from the session
/// before it — a numbered badge on the game page's trend charts, 1 for the oldest and the same number on every chart of the
/// page, and a row of the list under each chart. <see cref="Lines"/> are <see cref="TrendSeriesBuilder.Describe"/>'s texts
/// of that day, each once, in the order they were found.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture, which changes at runtime")]
public sealed record ChangeMarker(int Number, DateTimeOffset At, IReadOnlyList<string> Lines)
{
    /// <summary>The number as the badges show it: digits, whatever the language — text drawn on a chart is invariant.</summary>
    public string NumberText => Number.ToString(CultureInfo.InvariantCulture);

    /// <summary>The day, as the Sessions grid writes its dates without the time.</summary>
    public string DateText => Formats.Day(At);

    /// <summary>Every change of the day, one per line.</summary>
    public string Text => string.Join(Environment.NewLine, Lines);

    /// <summary>The tooltip near the marker: the number and the day, then the changes.</summary>
    public string ToolTipText => string.Format(CultureInfo.CurrentCulture, Strings.Trend_Change_Heading_Format, NumberText, DateText) + Environment.NewLine + Text;
}

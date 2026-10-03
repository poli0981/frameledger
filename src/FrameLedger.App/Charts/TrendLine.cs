namespace FrameLedger.App.Charts;

/// <summary>
/// One metric's line on a trend (beta.11, owner decision D39: several metrics at once): its label, the unit that decides
/// which axis it is drawn against, and its points (one per session, oldest first).
/// </summary>
/// <param name="Label">The metric, as the legend names it.</param>
/// <param name="Unit">The axis label every line in the same unit shares.</param>
/// <param name="Points">The sessions that have the metric.</param>
public sealed record TrendLine(string Label, string Unit, IReadOnlyList<TrendPoint> Points);

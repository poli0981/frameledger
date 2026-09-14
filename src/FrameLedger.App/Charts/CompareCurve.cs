namespace FrameLedger.App.Charts;

/// <summary>One session's percentile curve on the Compare overlay: its legend label and the 101 points.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "a data carrier for the chart; ScottPlot draws double[]")]
public sealed record CompareCurve(string Label, double[] Xs, double[] Ys);

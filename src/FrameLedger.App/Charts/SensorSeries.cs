namespace FrameLedger.App.Charts;

/// <summary>One decoded sensor series (<c>sensor_blobs</c>): its name, seconds from the session start, values with −1 (not read) removed.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "a data carrier for the charts: ScottPlot draws double[] and a 500k-frame session is decimated per draw, not copied per read")]
public sealed record SensorSeries(string Name, double[] TimesS, double[] Values);

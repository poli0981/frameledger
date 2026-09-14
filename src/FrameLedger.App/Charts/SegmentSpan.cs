namespace FrameLedger.App.Charts;

/// <summary>One <c>session_segments</c> row on the time axis: where it starts and ends in seconds, and what it was.</summary>
public sealed record SegmentSpan(double StartS, double EndS, string Label);

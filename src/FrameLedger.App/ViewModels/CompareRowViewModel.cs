namespace FrameLedger.App.ViewModels;

/// <summary>One row of Compare's stat table: the metric and one cell per compared session, in the selection's order.</summary>
public sealed record CompareRowViewModel(string Metric, IReadOnlyList<CompareCell> Cells);

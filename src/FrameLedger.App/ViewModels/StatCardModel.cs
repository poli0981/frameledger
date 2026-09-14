namespace FrameLedger.App.ViewModels;

/// <summary>One stat card of the session summary: a label and its value as text (N/A where the tier has none).</summary>
public sealed record StatCardModel(string Label, string Value, string? Suffix = null);

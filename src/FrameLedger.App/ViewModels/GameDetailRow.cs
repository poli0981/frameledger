namespace FrameLedger.App.ViewModels;

/// <summary>One line of the game page's Details card (beta.8): a label and what the game's files — or its store — say.</summary>
public sealed record GameDetailRow(string Label, string Value);

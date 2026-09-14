namespace FrameLedger.App.ViewModels;

/// <summary>One cell of Compare's stat table: the text, and whether it is the best of its row (never for N/A).</summary>
public sealed record CompareCell(string Text, bool IsBest);

namespace FrameLedger.App.ViewModels;

/// <summary>A value and the text it shows as, for a ComboBox bound by value.</summary>
public sealed record Choice<T>(T Value, string Label);

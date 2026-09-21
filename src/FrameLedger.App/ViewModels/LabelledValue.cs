using System.Runtime.InteropServices;

namespace FrameLedger.App.ViewModels;

/// <summary>One stated fact: what it is, and its value as text (N/A included).</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct LabelledValue(string Label, string Value);

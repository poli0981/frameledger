// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>One cell of Compare's stat table: the text, and whether it is the best of its row (never for N/A).</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture, which changes at runtime")]
public sealed record CompareCell(string Text, bool IsBest)
{
    /// <summary>The cell as a screen reader reads it (beta.35, D88): bold says "the best" to the eye, these words to the ear.</summary>
    public override string ToString() => IsBest ? string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.A11y_Compare_Best_Format, Text) : Text;
}

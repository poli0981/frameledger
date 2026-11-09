// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>A value and the text it shows as, for a ComboBox bound by value.</summary>
public sealed record Choice<T>(T Value, string Label)
{
    /// <summary>
    /// What a screen reader reads for the option (beta.35, D88): WPF names a drop-down's item by its <c>ToString()</c>, and a
    /// record's own was <c>Choice { Value = dark, Label = Dark }</c>.
    /// </summary>
    public override string ToString() => Label;
}

// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Controls;

/// <summary>
/// <c>08_UI</c> §Tri-state feature chips: Yes = filled accent, No = outlined, N/A = dashed outline with muted text;
/// the source (measured / manual / inherited) is the tooltip. A click opens FR-8.3's override dialog
/// (<c>TriStateOverridePrompt</c>); this said a flyout, which was never built.
/// </summary>
public sealed class TriStateChip : Control
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(TriStateChipModel), typeof(TriStateChip), new PropertyMetadata(null));

    static TriStateChip() => DefaultStyleKeyProperty.OverrideMetadata(typeof(TriStateChip), new FrameworkPropertyMetadata(typeof(TriStateChip)));

    public TriStateChipModel? Model
    {
        get => (TriStateChipModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }
}

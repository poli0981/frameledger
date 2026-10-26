// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;

namespace FrameLedger.App.Charts;

/// <summary>The list under a trend chart (beta.30, owner decision D70): each numbered change marker's day and changes in full.</summary>
public partial class ChangeMarkerList : UserControl
{
    public static readonly DependencyProperty MarkersProperty = DependencyProperty.Register(
        nameof(Markers), typeof(IReadOnlyList<ChangeMarker>), typeof(ChangeMarkerList), new PropertyMetadata(Array.Empty<ChangeMarker>()));

    public ChangeMarkerList() => InitializeComponent();

    public IReadOnlyList<ChangeMarker> Markers
    {
        get => (IReadOnlyList<ChangeMarker>)GetValue(MarkersProperty);
        set => SetValue(MarkersProperty, value);
    }
}

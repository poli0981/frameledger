// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>A value and the text it shows as, for a ComboBox bound by value.</summary>
public sealed record Choice<T>(T Value, string Label);

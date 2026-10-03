// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.App.ViewModels;

/// <summary>One stated fact: what it is, and its value as text (N/A included).</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct LabelledValue(string Label, string Value);

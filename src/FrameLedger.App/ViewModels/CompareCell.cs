// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>One cell of Compare's stat table: the text, and whether it is the best of its row (never for N/A).</summary>
public sealed record CompareCell(string Text, bool IsBest);

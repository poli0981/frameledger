// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>A session recorded with Advanced metrics on, in the selector: its id, and its game and start in words.</summary>
public sealed record AdvancedSessionOption(long SessionId, string Text)
{
    public override string ToString() => Text;
}

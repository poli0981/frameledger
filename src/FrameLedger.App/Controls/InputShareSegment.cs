// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Domain.Input;

namespace FrameLedger.App.Controls;

/// <summary>One segment of <see cref="InputShareBar"/> (0.1.0-beta.50): what the time was played with, and its share.</summary>
/// <param name="Class">A controller, the keyboard or mouse, both at once, or no input.</param>
/// <param name="Share">Its share of the time the game was in front (0..1); the bar draws the segments in proportion.</param>
public sealed record InputShareSegment(InputClass Class, double Share);

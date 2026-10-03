// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Domain.Metrics;

namespace FrameLedger.App.Services;

/// <summary>FR-8.3's answer: the override (null = use the measured value) and whether it is also the game's default.</summary>
public sealed record TriStateOverrideChoice(Tri? Override, bool SetAsGameDefault);

// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Persistence;

/// <summary>One on-demand retention sweep across every game: the games that have sessions, and the sessions whose raw series went.</summary>
public sealed record RetentionSweepResult(int Games, int Sessions);

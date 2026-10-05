// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Recording;

/// <summary>What the crash witnesses said about one session (beta.14): the Application log, and the crash reporter the game started, counted or only seen.</summary>
internal readonly record struct CrashWitness(bool ApplicationLog, string? Reporter, bool ReporterCounted);

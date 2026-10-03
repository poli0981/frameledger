// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Import;

/// <summary>What one import did: rows added (hooking off, every one), and candidates left alone (already there, or no executable).</summary>
public sealed record ImportReport(int Added, int Skipped);

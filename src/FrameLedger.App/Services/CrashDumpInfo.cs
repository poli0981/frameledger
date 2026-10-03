// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>The newest minidump the bug bundle could carry (P4 PR-9): where it is, when it was written, how big it is.</summary>
public sealed record CrashDumpInfo(string Path, DateTimeOffset WrittenAt, long Bytes);

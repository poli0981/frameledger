// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Persistence;

/// <summary>One accepted legal document: which version, and when.</summary>
public sealed record LegalAcceptance(string Document, string Version, DateTimeOffset AcceptedAt);

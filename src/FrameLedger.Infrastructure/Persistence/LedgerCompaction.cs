// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Infrastructure.Persistence;

/// <summary>What <see cref="LedgerMaintenance.CompactAsync"/> did to the file: the ledger's bytes (the database and its WAL) before and after.</summary>
public sealed record LedgerCompaction(long BytesBefore, long BytesAfter);

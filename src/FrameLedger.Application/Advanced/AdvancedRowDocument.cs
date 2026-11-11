// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>
/// An <see cref="AdvancedRow"/> as it is stored. Every member is nullable because a stored document is read
/// tolerantly: a ledger edited by hand can hold a row with no name, and the reader leaves that fact out rather than
/// trusting an annotation the text never promised to keep.
/// </summary>
internal sealed record AdvancedRowDocument
{
    public string? Name { get; init; }

    public string? Value { get; init; }

    public string? Token { get; init; }
}

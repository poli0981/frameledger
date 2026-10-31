// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Infrastructure.Persistence;

/// <summary>
/// An alpha build could not copy the ledger before opening it (D73), and so does not open it: the copy is the promise the
/// Alpha notice makes (§3), and a ledger the alpha wrote without one is exactly what it promised not to risk.
/// </summary>
public sealed class LedgerBackupException : Exception
{
    public LedgerBackupException()
    {
    }

    public LedgerBackupException(string message)
        : base(message)
    {
    }

    public LedgerBackupException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

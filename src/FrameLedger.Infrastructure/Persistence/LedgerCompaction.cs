namespace FrameLedger.Infrastructure.Persistence;

/// <summary>What <see cref="LedgerMaintenance.CompactAsync"/> did to the file: the ledger's bytes (the database and its WAL) before and after.</summary>
public sealed record LedgerCompaction(long BytesBefore, long BytesAfter);

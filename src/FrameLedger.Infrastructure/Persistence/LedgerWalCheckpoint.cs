namespace FrameLedger.Infrastructure.Persistence;

/// <summary>
/// What one <c>PRAGMA wal_checkpoint</c> reported: SQLite's three columns, named. <paramref name="WalFrames"/> is the
/// number of frames the WAL held (−1 when the database is not in WAL mode); <paramref name="Checkpointed"/> is how
/// many of them reached the main file; <paramref name="Busy"/> is true when another connection kept it from finishing.
/// A read-write open records one of these (<see cref="LedgerDatabase.OpenDiagnostics"/>) and a close reports another.
/// </summary>
public sealed record LedgerWalCheckpoint(string Mode, bool Busy, long WalFrames, long Checkpointed)
{
    /// <summary>True when every frame the WAL held is now in the main file.</summary>
    public bool Complete => !Busy && WalFrames <= Checkpointed;

    public override string ToString() =>
        $"{Mode}: {WalFrames} frame(s) in the wal, {Checkpointed} checkpointed{(Busy ? ", busy" : string.Empty)}";
}

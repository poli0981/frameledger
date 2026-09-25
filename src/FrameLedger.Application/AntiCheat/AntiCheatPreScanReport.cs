namespace FrameLedger.Application.AntiCheat;

/// <summary>What one pre-scan pass did.</summary>
public sealed record AntiCheatPreScanReport
{
    public int Found { get; init; }
    public int Clean { get; init; }
    public int Unverified { get; init; }
    public int Blocked { get; init; }
    public int Current { get; init; }
    public int Unreadable { get; init; }
    public bool RulesUnusable { get; init; }

    /// <summary>D33: blocked entries whose user-mode exception eligibility the pass (re)wrote.</summary>
    public int ExceptionsChecked { get; init; }

    /// <summary>D33: exceptions the pass ended — an executable that changed, a game no longer eligible.</summary>
    public int ExceptionsEnded { get; init; }

    public int Scanned => Found + Clean + Unverified;
}

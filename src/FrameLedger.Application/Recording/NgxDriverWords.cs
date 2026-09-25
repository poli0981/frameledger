namespace FrameLedger.Application.Recording;

/// <summary>What <c>sessions.ngx_driver_words</c> stores: the driver's raw words as probed, plus how the probing went.</summary>
public sealed record NgxDriverWords
{
    public required string Outcome { get; init; }

    public string? Sr { get; init; }

    public string? Rr { get; init; }

    public string? Fg { get; init; }

    public uint? Driver { get; init; }

    // beta.8 (2026-09-25): the rest of what NvAPI_NGX_GetNGXOverrideState answers, kept since this date so the summary
    // can say what an override the driver reports actually set. Null on a row written before, and unless answered.

    /// <summary>The scaling ratio the driver reports for super resolution.</summary>
    public float? Ratio { get; init; }

    /// <summary>The performance mode the driver reports (<c>NGX_DLSS_SR_MODE</c>'s numbering when an override set it).</summary>
    public uint? Mode { get; init; }

    /// <summary>The render preset for super resolution / ray reconstruction (1 = A … 15 = O).</summary>
    public uint? Preset { get; init; }

    /// <summary>The frame-generation override's frame count target.</summary>
    public uint? FgCount { get; init; }

    /// <summary>The frame-generation render preset.</summary>
    public uint? FgPreset { get; init; }

    /// <summary>The frame-generation mode.</summary>
    public uint? FgMode { get; init; }

    public int Readings { get; init; }

    public int Answered { get; init; }

    public bool Changed { get; init; }

    public string? Detail { get; init; }
}

namespace FrameLedger.Application.TriState;

/// <summary>FR-8.1's <c>source</c>: where a resolved tri-state value came from, so the UI can draw it distinctly (FR-8.3).</summary>
public enum TriStateSource
{
    /// <summary>Nothing measured, nothing overridden, no game default: the honest <c>N/A</c>.</summary>
    NotApplicable = 0,

    /// <summary>The session's own measurement (the writer's evidence, <c>rt_source = measured</c>).</summary>
    Measured,

    /// <summary>A per-session manual override (the UI-owned <c>session_annotations</c> row).</summary>
    Manual,

    /// <summary>The game's default (<c>games.rt_default</c> etc.), inherited because the session measured nothing.</summary>
    Inherited,
}

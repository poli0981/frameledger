namespace FrameLedger.App.Services;

/// <summary>The three shapes of <c>08_UI</c> §FPS display rule (CLAUDE.md rule 6).</summary>
public enum FpsReadoutKind
{
    /// <summary>Frame generation was not measured: the one number is Presented FPS with a mandatory census qualifier.</summary>
    Presented,

    /// <summary>Frame generation was measured as none: the number stands alone, nothing else.</summary>
    None,

    /// <summary>Frame generation was measured: Native first, Displayed second, the factor as a chip.</summary>
    Generated,

    /// <summary>Nothing to show (a Tier 2 session, or no frames yet).</summary>
    Unavailable,
}

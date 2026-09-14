namespace FrameLedger.App.Services;

/// <summary>The shapes of <c>08_UI</c> §FPS display rule (CLAUDE.md rule 6).</summary>
public enum FpsReadoutKind
{
    /// <summary>Frame generation was not measured: the one number is Presented FPS with a mandatory census qualifier.</summary>
    Presented,

    /// <summary>Frame generation was measured as none: the number stands alone, nothing else.</summary>
    None,

    /// <summary>Frame generation was measured and counted: Native first, Displayed second, the factor as a chip.</summary>
    Generated,

    /// <summary>
    /// Frame generation was IDENTIFIED (a hook named the technology) and the count refused, so no factor exists
    /// (<c>fg_mode</c> a technology, <c>fg_factor</c> NULL, <c>fg_refusal</c> the reason — schema 0003). The one number
    /// is Presented FPS with a warning chip naming the technology: it may include generated frames, and "Native"
    /// never appears. Added 2026-09-14 after two of the owner's rows took this shape and every FG surface said N/A.
    /// </summary>
    IdentifiedUncounted,

    /// <summary>Nothing to show (a Tier 2 session, or no frames yet).</summary>
    Unavailable,
}

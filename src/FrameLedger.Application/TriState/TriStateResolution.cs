using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.Application.TriState;

/// <summary>
/// FR-8.3 as one rule: a manual override wins; else the session's own measurement when it measured anything;
/// else the game's default, marked inherited; else <c>N/A</c>. The measurement is never overwritten — the
/// override lives beside it on the UI-owned annotation row (<c>06_DATA_MODEL</c> §Writer ownership), so
/// clearing an override restores what was measured rather than guessing at it.
/// </summary>
public static class TriStateResolution
{
    public static ResolvedTriState Resolve(Tri? manualOverride, Tri measured, Tri gameDefault)
    {
        if (manualOverride is Tri chosen)
        {
            return new ResolvedTriState(chosen, TriStateSource.Manual);
        }

        if (measured != Tri.NotApplicable)
        {
            return new ResolvedTriState(measured, TriStateSource.Measured);
        }

        return gameDefault != Tri.NotApplicable
            ? new ResolvedTriState(gameDefault, TriStateSource.Inherited)
            : ResolvedTriState.NotApplicable;
    }

    /// <summary>One feature of a stored session, its annotation (may be null) and its game.</summary>
    public static ResolvedTriState Resolve(TriStateKind kind, SessionRow session, SessionAnnotation? annotation, GameRow game)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(game);
        return kind switch
        {
            TriStateKind.RayTracing => Resolve(annotation?.RtOverride, Vocabulary.ParseTri(session.RtFlag), game.RtDefault),
            TriStateKind.PathTracing => Resolve(annotation?.PtOverride, Vocabulary.ParseTri(session.PtFlag), game.PtDefault),
            TriStateKind.RayReconstruction => Resolve(annotation?.RrOverride, Vocabulary.ParseTri(session.RrFlag), game.RrDefault),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "not a tri-state kind"),
        };
    }
}

namespace FrameLedger.Application.TriState;

/// <summary>The three tri-state features of FR-8 (<c>03_METRICS</c> §RT / PT / RR).</summary>
public enum TriStateKind
{
    RayTracing,
    PathTracing,
    RayReconstruction,
}

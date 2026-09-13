using System.Runtime.InteropServices;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.Application.TriState;

/// <summary>A tri-state value and where it came from.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct ResolvedTriState(Tri Value, TriStateSource Source)
{
    public static ResolvedTriState NotApplicable => new(Tri.NotApplicable, TriStateSource.NotApplicable);
}

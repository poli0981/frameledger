using FrameLedger.Application.TriState;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.App.ViewModels;

/// <summary>One RT / PT / RR chip (<c>08_UI</c> §Tri-state feature chips): the label, the value, and where it came from (the tooltip).</summary>
public sealed record TriStateChipModel(string Label, Tri Value, TriStateSource Source)
{
    /// <summary>Which feature this chip is, for the override that writes it back.</summary>
    public TriStateKind Kind { get; init; } = TriStateKind.RayTracing;

    public string ValueText => Value switch
    {
        Tri.Yes => Strings.Chip_Yes,
        Tri.No => Strings.Chip_No,
        _ => Strings.Chip_NA,
    };

    public string SourceText => Source switch
    {
        TriStateSource.Measured => Strings.Chip_Source_Measured,
        TriStateSource.Manual => Strings.Chip_Source_Manual,
        TriStateSource.Inherited => Strings.Chip_Source_Inherited,
        _ => Strings.Chip_Source_NA,
    };

    public string Text => Label + ": " + ValueText;

    public bool IsYes => Value == Tri.Yes;

    public bool IsNo => Value == Tri.No;

    public bool IsNotApplicable => Value == Tri.NotApplicable;

    public static TriStateChipModel Of(TriStateKind kind, ResolvedTriState resolved) => new(LabelOf(kind), resolved.Value, resolved.Source) { Kind = kind };

    public static string LabelOf(TriStateKind kind) => kind switch
    {
        TriStateKind.RayTracing => Strings.Chip_Rt,
        TriStateKind.PathTracing => Strings.Chip_Pt,
        _ => Strings.Chip_Rr,
    };
}

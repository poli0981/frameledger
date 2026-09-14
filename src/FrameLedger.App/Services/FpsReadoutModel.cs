namespace FrameLedger.App.Services;

/// <summary>What an <c>FpsReadout</c> shows, decided once by <see cref="FpsPresentation"/> and bound by the control.</summary>
public sealed record FpsReadoutModel
{
    public static readonly FpsReadoutModel Unavailable = new() { Kind = FpsReadoutKind.Unavailable };

    public required FpsReadoutKind Kind { get; init; }

    public double? Native { get; init; }

    public double? Displayed { get; init; }

    public double? Factor { get; init; }

    public double? Presented { get; init; }

    public FpsQualifier? Qualifier { get; init; }

    /// <summary>The primary figure as text: Native (generated), the number (none), Presented (not measured), or N/A.</summary>
    public string PrimaryText => Kind switch
    {
        FpsReadoutKind.Generated => Formats.Fps(Native),
        FpsReadoutKind.None => Formats.Fps(Native),
        FpsReadoutKind.Presented => Formats.Fps(Presented),
        _ => Strings.Common_NotAvailable,
    };

    /// <summary>The one line, per the rule: <c>62 → 118 FPS (×1.9 FG)</c>, or <c>144 FPS</c>.</summary>
    public string Line => Kind switch
    {
        FpsReadoutKind.Generated => FpsPresentation.GeneratedLine(Native, Displayed, Factor),
        FpsReadoutKind.None => FpsPresentation.PresentedLine(Native),
        FpsReadoutKind.Presented => FpsPresentation.PresentedLine(Presented),
        _ => Strings.Common_NotAvailable,
    };

    public string? QualifierText => Kind == FpsReadoutKind.Presented && Qualifier is FpsQualifier q ? FpsPresentation.QualifierText(q) : null;

    public string? QualifierTooltip => Kind == FpsReadoutKind.Presented && Qualifier is FpsQualifier q ? FpsPresentation.QualifierTooltip(q) : null;

    public bool QualifierIsWarning => Qualifier is FpsQualifier.RuntimeLoaded or FpsQualifier.Withheld;

    public string? FactorChip => Kind == FpsReadoutKind.Generated && Factor is double f ? FpsPresentation.FactorChip(f) : null;
}

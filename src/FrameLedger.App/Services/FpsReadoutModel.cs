namespace FrameLedger.App.Services;

/// <summary>What an <c>FpsReadout</c> shows, decided once by <see cref="FpsPresentation"/> and bound by the control.</summary>
public sealed record FpsReadoutModel
{
    public static readonly FpsReadoutModel Unavailable = new() { Kind = FpsReadoutKind.Unavailable };

    public required FpsReadoutKind Kind { get; init; }

    public double? Native { get; init; }

    public double? Displayed { get; init; }

    /// <summary>The counted factor; present on every <see cref="FpsReadoutKind.Generated"/> model and on no other.</summary>
    public double? Factor { get; init; }

    public double? Presented { get; init; }

    public FpsQualifier? Qualifier { get; init; }

    /// <summary>The technology's product name (<c>DLSS-G</c>, <c>FSR FG</c>, …) when one was identified; the chip's subject on <see cref="FpsReadoutKind.IdentifiedUncounted"/>.</summary>
    public string? Technology { get; init; }

    /// <summary>The row's <c>fg_refusal</c> token when the count refused; the chip's tooltip names it.</summary>
    public string? Refusal { get; init; }

    /// <summary>The row's <c>fg_refusal_detail</c> JSON (schema 0005) when stored; the tooltip's numbers. Null on the live card.</summary>
    public string? RefusalDetail { get; init; }

    /// <summary>The raw runtime census, so a <c>fg_runtime_loaded</c> chip can name the module (<c>08_UI</c> §FPS display rule).</summary>
    public long? RuntimeCensus { get; init; }

    /// <summary>The primary figure as text: Native (generated), the number (none), Presented (not measured, or identified and uncounted), or N/A.</summary>
    public string PrimaryText => Kind switch
    {
        FpsReadoutKind.Generated => Formats.Fps(Native),
        FpsReadoutKind.None => Formats.Fps(Native),
        FpsReadoutKind.Presented or FpsReadoutKind.IdentifiedUncounted => Formats.Fps(Presented),
        _ => Strings.Common_NotAvailable,
    };

    /// <summary>The one line, per the rule: <c>62 → 118 FPS (×1.9 FG)</c>, or <c>144 FPS</c>. Never a factor that was not counted.</summary>
    public string Line => Kind switch
    {
        FpsReadoutKind.Generated when Factor is double f => FpsPresentation.GeneratedLine(Native, Displayed, f),
        FpsReadoutKind.None => FpsPresentation.PresentedLine(Native),
        FpsReadoutKind.Presented or FpsReadoutKind.IdentifiedUncounted => FpsPresentation.PresentedLine(Presented),
        _ => Strings.Common_NotAvailable,
    };

    /// <summary>The chip under a stand-alone number: the census qualifier, or the identified technology with "factor not counted".</summary>
    public string? QualifierText => Kind switch
    {
        FpsReadoutKind.Presented when Qualifier is FpsQualifier q => FpsPresentation.QualifierText(q, RuntimeCensus),
        FpsReadoutKind.IdentifiedUncounted => FpsPresentation.IdentifiedText(Technology),
        _ => null,
    };

    public string? QualifierTooltip => Kind switch
    {
        FpsReadoutKind.Presented when Qualifier is FpsQualifier q => FpsPresentation.QualifierTooltip(q),
        FpsReadoutKind.IdentifiedUncounted => FpsPresentation.IdentifiedTooltip(Technology, Refusal, RefusalDetail),
        _ => null,
    };

    /// <summary>True when the stand-alone number may include generated frames and the chip must warn.</summary>
    public bool QualifierIsWarning => Kind == FpsReadoutKind.IdentifiedUncounted || Qualifier is FpsQualifier.RuntimeLoaded or FpsQualifier.Withheld;

    public string? FactorChip => Kind == FpsReadoutKind.Generated && Factor is double f ? FpsPresentation.FactorChip(f) : null;
}

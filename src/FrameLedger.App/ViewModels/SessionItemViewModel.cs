using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.TriState;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// One row of the Sessions tab (FR-6.1) as text, decided once: the tier badge, the three FPS columns under
/// <see cref="FpsPresentation"/> (<c>—</c> for a measured none, <c>N/A</c> for not measured, never blank, never
/// zero — FR-4.9), the lows, resolution · upscaler, the chips resolved with the annotation and the game default.
/// </summary>
public sealed class SessionItemViewModel
{
    public SessionItemViewModel(SessionRow row, SessionAnnotation? annotation, GameRow game)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(game);
        Row = row;
        Id = row.Id;
        DateText = Formats.Date(row.StartedAt);
        DurationText = Formats.Duration(row.DurationSeconds);
        IsHooked = row.Tier == CaptureTier.Hooked;
        TierText = IsHooked ? Strings.Tier_Hooked : Strings.Tier_NotHooked;
        TierTooltip = IsHooked ? Strings.Tier_Hooked_Tooltip : Strings.Tier_NotHooked_Tooltip;
        NativeText = FpsPresentation.NativeColumn(row);
        DisplayedText = FpsPresentation.DisplayedColumn(row);
        FgText = FpsPresentation.FactorColumn(row);
        FpsTooltip = FpsPresentation.ColumnTooltip(row);
        P1LowText = IsHooked ? Formats.Fps(row.P1LowFps) : Strings.Common_NotAvailable;
        P01LowText = IsHooked ? Formats.Fps(row.P01LowFps) : Strings.Common_NotAvailable;
        ResolutionText = IsHooked ? Formats.Resolution(row.RenderW, row.RenderH, row.OutputW, row.OutputH) + " · " + Formats.Upscaler(row.Upscaler, row.UpscalerQuality, row.UpscalerDriverReported) : Strings.Common_NotAvailable;
        GpuTempText = Formats.Temperature(row.MaxGpuTemp);
        ApiText = IsHooked ? Formats.Api(row.Api) : Strings.Common_NotAvailable;
        ExitText = Formats.ExitStatusText(row.ExitStatus);
        IsCrashed = row.ExitStatus == ExitStatus.Crashed;
        TagsText = annotation is null ? string.Empty : string.Join(", ", annotation.Tags);
        Rt = TriStateChipModel.Of(TriStateKind.RayTracing, TriStateResolution.Resolve(TriStateKind.RayTracing, row, annotation, game));
        Pt = TriStateChipModel.Of(TriStateKind.PathTracing, TriStateResolution.Resolve(TriStateKind.PathTracing, row, annotation, game));
        Rr = TriStateChipModel.Of(TriStateKind.RayReconstruction, TriStateResolution.Resolve(TriStateKind.RayReconstruction, row, annotation, game));
    }

    public SessionRow Row { get; }

    public long Id { get; }

    public string DateText { get; }

    public string DurationText { get; }

    public bool IsHooked { get; }

    public string TierText { get; }

    public string TierTooltip { get; }

    public string NativeText { get; }

    public string DisplayedText { get; }

    public string FgText { get; }

    public string? FpsTooltip { get; }

    public string P1LowText { get; }

    public string P01LowText { get; }

    public string ResolutionText { get; }

    public string GpuTempText { get; }

    public string ApiText { get; }

    public string ExitText { get; }

    public bool IsCrashed { get; }

    public string TagsText { get; }

    public TriStateChipModel Rt { get; }

    public TriStateChipModel Pt { get; }

    public TriStateChipModel Rr { get; }
}

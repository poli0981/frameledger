using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FrameLedger.App.Services;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// The Dashboard's live capture card (<c>08_UI</c> §Dashboard), fed by <c>SessionStarted</c> / <c>SessionProgress</c> /
/// <c>SessionCompleted</c>. Tier 1 shows measured settings as facts; the readout follows the FPS display rule at
/// 1 Hz, qualifier included. The sparkline is PR-6's.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed partial class LiveCaptureViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _waitingForFrames;

    [ObservableProperty]
    private string _gameName = string.Empty;

    [ObservableProperty]
    private string _tierText = string.Empty;

    [ObservableProperty]
    private string _elapsedText = string.Empty;

    [ObservableProperty]
    private FpsReadoutModel _readout = FpsReadoutModel.Unavailable;

    [ObservableProperty]
    private string _resolutionText = string.Empty;

    [ObservableProperty]
    private string _upscalerText = string.Empty;

    [ObservableProperty]
    private string _fgText = string.Empty;

    [ObservableProperty]
    private bool _rtActive;

    [ObservableProperty]
    private string _gpuTempText = string.Empty;

    [ObservableProperty]
    private string _cpuTempText = string.Empty;

    [ObservableProperty]
    private string _vramText = string.Empty;

    public Guid? SessionGuid { get; private set; }

    public static string Header => Strings.Dashboard_Live_Header;

    public static string IdleText => Strings.Dashboard_Live_Idle;

    public static string WaitingText => Strings.Dashboard_Live_Waiting;

    public static string RtOnText => Strings.Dashboard_Live_Rt_On;

    public void Start(SessionStartedEvent started)
    {
        ArgumentNullException.ThrowIfNull(started);
        SessionGuid = started.SessionGuid;
        GameName = started.GameName ?? Strings.Common_NotAvailable;
        TierText = started.Tier == 1 ? Strings.Tier_Hooked : Strings.Tier_NotHooked;
        ElapsedText = string.Empty;
        Readout = FpsReadoutModel.Unavailable;
        ResolutionText = UpscalerText = FgText = GpuTempText = CpuTempText = VramText = string.Empty;
        RtActive = false;
        WaitingForFrames = true;
        IsActive = true;
    }

    public void Update(SessionProgressEvent progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (SessionGuid is Guid guid && guid != progress.SessionGuid)
        {
            return;
        }

        SessionGuid ??= progress.SessionGuid;
        IsActive = true;
        WaitingForFrames = progress.Presents5s == 0;
        ElapsedText = string.Format(CultureInfo.CurrentCulture, Strings.Dashboard_Live_Elapsed_Format, Formats.Duration(progress.ElapsedS));
        Readout = FpsPresentation.FromProgress(progress);
        ResolutionText = Formats.Resolution(progress.RenderW, progress.RenderH, progress.OutputW, progress.OutputH);
        UpscalerText = Formats.Upscaler(progress.Upscaler, progress.UpscalerQuality, progress.UpscalerDriverReported);
        FgText = FpsPresentation.FrameGenerationLabel(Readout, progress.FgMode);
        RtActive = progress.RtActive == true;
        GpuTempText = progress.GpuTempC is double g ? string.Format(CultureInfo.CurrentCulture, Strings.Dashboard_Live_GpuTemp_Format, Math.Round(g)) : string.Empty;
        CpuTempText = progress.CpuTempC is double c ? string.Format(CultureInfo.CurrentCulture, Strings.Dashboard_Live_CpuTemp_Format, Math.Round(c)) : string.Empty;
        VramText = progress.VramProcMb is int v ? string.Format(CultureInfo.CurrentCulture, Strings.Dashboard_Live_Vram_Format, v) : string.Empty;
    }

    public void Stop(SessionCompletedEvent completed)
    {
        ArgumentNullException.ThrowIfNull(completed);
        if (SessionGuid is Guid guid && guid != completed.SessionGuid)
        {
            return;
        }

        SessionGuid = null;
        IsActive = false;
        WaitingForFrames = false;
    }
}

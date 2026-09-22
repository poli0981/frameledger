using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FrameLedger.App.Services;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// The Dashboard's live capture card (<c>08_UI</c> §Dashboard), fed by <c>SessionStarted</c> / <c>SessionProgress</c> /
/// <c>SessionHeld</c> / <c>SessionCompleted</c>. Tier 1 shows measured settings as facts; the readout follows the FPS
/// display rule at 1 Hz, qualifier included. The sparkline is PR-6's.
/// </summary>
/// <remarks>
/// <b>A session that is not measured is shown too (2026-09-23).</b> Its name, its tier, how long it has run, the machine's
/// temperatures, and why nothing is measured — the same words its summary will use — with no readout at all. Until this
/// date the Agent announced only hooked sessions, and a hooking-off or refused game read "Nothing is being captured." for
/// as long as it ran. When two sessions run, a hooked one takes the card.
/// </remarks>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed partial class LiveCaptureViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _waitingForFrames;

    /// <summary>A Tier-2 session: recorded, not measured.</summary>
    [ObservableProperty]
    private bool _isHeld;

    /// <summary>Why a held session measures nothing: the sentence its summary will carry.</summary>
    [ObservableProperty]
    private string _heldReason = string.Empty;

    public static string HeldText => Strings.Dashboard_Live_RecordingOnly;

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

    /// <summary>The tier of the session on the card; 0 when there is none.</summary>
    public int Tier { get; private set; }

    public void Start(SessionStartedEvent started)
    {
        ArgumentNullException.ThrowIfNull(started);
        Show(started.SessionGuid, started.GameName, started.Tier, started.Hold);
    }

    /// <summary>A session that was already running when the card was built (the Dashboard is rebuilt on every visit).</summary>
    public void Seed(RunningSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Show(session.SessionGuid, session.GameName, session.Tier, session.Hold);
    }

    /// <summary>A held session's 1 Hz tick: elapsed and the machine's temperatures.</summary>
    public void Hold(SessionHeldEvent held)
    {
        ArgumentNullException.ThrowIfNull(held);
        if (SessionGuid != held.SessionGuid)
        {
            return;
        }

        ElapsedText = string.Format(CultureInfo.CurrentCulture, Strings.Dashboard_Live_Elapsed_Format, Formats.Duration(held.ElapsedS));
        GpuTempText = held.GpuTempC is double g ? string.Format(CultureInfo.CurrentCulture, Strings.Dashboard_Live_GpuTemp_Format, Math.Round(g)) : string.Empty;
        CpuTempText = held.CpuTempC is double c ? string.Format(CultureInfo.CurrentCulture, Strings.Dashboard_Live_CpuTemp_Format, Math.Round(c)) : string.Empty;
    }

    private void Show(Guid sessionGuid, string? gameName, int tier, SessionHold? hold)
    {
        // A hooked session takes the card from an unhooked one, never the other way round.
        if (IsActive && SessionGuid is Guid shown && shown != sessionGuid && (Tier == 1 || tier != 1))
        {
            return;
        }

        SessionGuid = sessionGuid;
        Tier = tier;
        GameName = gameName ?? Strings.Common_NotAvailable;
        TierText = tier == 1 ? Strings.Tier_Hooked : Strings.Tier_NotHooked;
        ElapsedText = string.Empty;
        Readout = FpsReadoutModel.Unavailable;
        ResolutionText = UpscalerText = FgText = GpuTempText = CpuTempText = VramText = string.Empty;
        RtActive = false;

        // Held means the Agent said why (a Tier-2 start carries its hold). A session the status lists at tier 2 with no hold
        // has not attached YET — it is starting: "nothing is measured" would be a claim nobody made, and the waiting line
        // says "Attached", which it is not. It shows its name and badge until its attach's start arrives.
        SessionHold? why = tier != 1 ? hold : null;
        IsHeld = why is not null;
        HeldReason = why is null ? string.Empty : Formats.Tier2HoldReason(why);
        WaitingForFrames = tier == 1;
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
        Tier = 1;
        IsHeld = false;
        HeldReason = string.Empty;
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
        Tier = 0;
        IsActive = false;
        WaitingForFrames = false;
        IsHeld = false;
        HeldReason = GameName = TierText = ElapsedText = GpuTempText = CpuTempText = VramText = string.Empty;
    }
}

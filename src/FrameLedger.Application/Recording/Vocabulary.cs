using FrameLedger.Domain.Metrics;
using FrameLedger.Domain.Sessions;
using FrameLedger.Shared;

namespace FrameLedger.Application.Recording;

/// <summary>
/// The tokens <c>sessions</c> stores (<c>0001_init.sql</c> column comments and CHECKs), from the enums the
/// pipeline carries. One place, so the writer and every reader agree; never a display string.
/// </summary>
public static class Vocabulary
{
    public const string NotApplicable = "na";

    public const string Measured = "measured";

    /// <summary><c>upscaler</c>: none|dlss|fsr2|fsr3|fsr4|xess|nis|unknown, plus <c>fsr</c> for the SDK 2.x DLL that does not name its version.</summary>
    public static string Upscaler(FlUpscaler value) => value switch
    {
        FlUpscaler.Dlss => "dlss",
        FlUpscaler.Fsr2 => "fsr2",
        FlUpscaler.Fsr3 => "fsr3",
        FlUpscaler.Fsr4 => "fsr4",
        FlUpscaler.XeSS => "xess",
        FlUpscaler.Nis => "nis",
        FlUpscaler.None => "none",
        FlUpscaler.FsrUnversioned => "fsr",
        _ => "unknown",
    };

    public static string Upscaler(UpscalerKind value) => Upscaler((FlUpscaler)(byte)value);

    /// <summary><c>fg_mode</c>: the technology's token, <c>none</c>, <c>active</c> for a counted but unidentified generator, else <c>na</c>.</summary>
    public static string FgMode(FgVerdict verdict, FlFgMode? identity) => verdict switch
    {
        FgVerdict.Named => identity switch
        {
            FlFgMode.DlssG => "dlssg",
            FlFgMode.FsrFg => "fsrfg",
            FlFgMode.XeFg => "xefg",
            FlFgMode.None => "none",
            _ => "unknown",
        },
        FgVerdict.None or FgVerdict.NoneInputsTagged => "none",
        FgVerdict.ActiveUnidentified => "active",
        _ => NotApplicable,
    };

    /// <summary>
    /// <c>fg_refusal</c> (schema 0003): why no factor was published, as a token the UI can name a reason for;
    /// null for <see cref="FgRefusalKind.None"/>, which is the value a window carries when a factor stands.
    /// </summary>
    public static string? FgRefusal(FgRefusalKind kind) => kind switch
    {
        FgRefusalKind.NotCounted => "not_counted",
        FgRefusalKind.Unattributed => "unattributed",
        FgRefusalKind.MultipleStreams => "multiple_streams",
        FgRefusalKind.CountSaturated => "count_saturated",
        FgRefusalKind.DxgiSaturated => "dxgi_saturated",
        FgRefusalKind.NoEvaluations => "no_evaluations",
        FgRefusalKind.TooShortToCheck => "too_short",
        FgRefusalKind.NonUniform => "non_uniform",
        FgRefusalKind.AmbiguousBand => "ambiguous_band",
        FgRefusalKind.NoBatches => "no_batches",
        _ => null,
    };

    /// <summary><c>fg_source</c>: NULL = not measured; <c>api</c> a hooked identity; <c>cadence</c> the count alone; <c>none</c> the counted negative.</summary>
    public static string? FgSource(FgVerdict verdict) => verdict switch
    {
        FgVerdict.Named => "api",
        FgVerdict.ActiveUnidentified => "cadence",
        FgVerdict.None or FgVerdict.NoneInputsTagged => "none",
        _ => null,
    };

    public static string Tri(Tri value) => value switch
    {
        Domain.Metrics.Tri.Yes => "yes",
        Domain.Metrics.Tri.No => "no",
        _ => NotApplicable,
    };

    /// <summary>The inverse of <see cref="Tri(Domain.Metrics.Tri)"/>: anything but <c>yes</c>/<c>no</c> (a NULL, a hand edit) is <c>N/A</c>.</summary>
    public static Tri ParseTri(string? text) => text switch
    {
        "yes" => Domain.Metrics.Tri.Yes,
        "no" => Domain.Metrics.Tri.No,
        _ => Domain.Metrics.Tri.NotApplicable,
    };

    public static string? Api(FrameApi api) => api switch
    {
        FrameApi.D3D11 => "d3d11",
        FrameApi.D3D12 => "d3d12",
        FrameApi.Vulkan => "vulkan",
        FrameApi.OpenGL => "opengl",
        _ => null,
    };

    /// <summary><c>exit_status</c> (<c>04_CAPTURE</c> §Crash and exit classification); the column's writer and the pipe's <c>SessionCompleted</c> both spell it here.</summary>
    public static string ExitStatusText(ExitStatus status) => status switch
    {
        ExitStatus.Normal => "normal",
        ExitStatus.Crashed => "crashed",
        ExitStatus.UnhookedSafety => "unhooked_safety",
        ExitStatus.Degraded => "degraded",
        ExitStatus.Interrupted => "interrupted",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "not a sessions.exit_status value"),
    };
}

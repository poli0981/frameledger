namespace FrameLedger.Application.Capture;

/// <summary>
/// The NVIDIA driver (DRS) setting ids behind the NVIDIA App's per-game DLSS and frame-generation overrides, which
/// <see cref="IDriverProfileSource"/> reads (beta.8, 2026-09-25). The ids and the meanings of their values are NVIDIA's own
/// <c>NvApiDriverSettings.h</c> (github.com/NVIDIA/nvapi, MIT) — except <see cref="SmoothMotion"/>, which NVIDIA has not
/// published and which community profile tools (nvidiaProfileInspector) document; every surface that shows it says so.
/// </summary>
public static class NvidiaDriverSettings
{
    /// <summary><c>NGX_DLSS_SR_OVERRIDE</c>: 0 off, 1 on — the NVIDIA App's DLSS Super Resolution override.</summary>
    public const uint DlssSrOverride = 0x10E41E01;

    /// <summary><c>NGX_DLSS_RR_OVERRIDE</c>: 0 off, 1 on — its Ray Reconstruction override.</summary>
    public const uint DlssRrOverride = 0x10E41E02;

    /// <summary><c>NGX_DLSS_FG_OVERRIDE</c>: 0 off, 1 on — its Frame Generation override.</summary>
    public const uint DlssFgOverride = 0x10E41E03;

    /// <summary><c>SL_DLSS_OVERRIDE</c>: 0 off, 1 on — the override for a title that reaches DLSS through Streamline.</summary>
    public const uint StreamlineDlssOverride = 0x10E41E06;

    /// <summary><c>NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION</c>: 0 off, 1–15 preset A–O, <see cref="PresetLatest"/>.</summary>
    public const uint DlssSrPreset = 0x10E41DF3;

    /// <summary><c>NGX_DLSS_RR_OVERRIDE_RENDER_PRESET_SELECTION</c>: 0 off, 1–15 preset A–O, <see cref="PresetLatest"/>.</summary>
    public const uint DlssRrPreset = 0x10E41DF7;

    /// <summary><c>NGX_DLSS_FG_OVERRIDE_RENDER_PRESET_SELECTION</c>: 0 off, 1–26 preset A–Z, <see cref="PresetDefault"/>, <see cref="PresetLatest"/>.</summary>
    public const uint DlssFgPreset = 0x10E41DF1;

    /// <summary><c>NGX_DLSS_SR_MODE</c>: 0 Performance, 1 Balanced, 2 Quality, 3 the game's own (the default), 4 DLAA, 5 Ultra Performance, 6 custom.</summary>
    public const uint DlssSrMode = 0x10AFB768;

    /// <summary><c>NGX_DLSS_RR_MODE</c>: as <see cref="DlssSrMode"/>.</summary>
    public const uint DlssRrMode = 0x10BD9423;

    /// <summary><c>NGX_DLSS_SR_OVERRIDE_SCALING_RATIO</c>: 33–100 (percent, the custom mode's), 0 the default.</summary>
    public const uint DlssSrScalingRatio = 0x10E41DF5;

    /// <summary><c>NGX_DLSS_RR_OVERRIDE_SCALING_RATIO</c>: as <see cref="DlssSrScalingRatio"/>.</summary>
    public const uint DlssRrScalingRatio = 0x10C7D4A2;

    /// <summary><c>NGX_DLAA_OVERRIDE</c>: 0 the default, 1 DLAA on.</summary>
    public const uint DlaaOverride = 0x10E41DF4;

    /// <summary><c>NGX_DLSSG_MULTI_FRAME_COUNT</c>: 0 off, 1–15 frames generated per rendered frame.</summary>
    public const uint DlssgMultiFrameCount = 0x104D6667;

    /// <summary><c>NGX_DLSSG_MODE</c>: 0 not set (the default), 1 off, 2 on, 3 auto, 4 dynamic.</summary>
    public const uint DlssgMode = 0x10308298;

    /// <summary><c>NGX_DLSSG_DYNAMIC_MULTI_FRAME_COUNT_MAX</c>: 0 off, else the dynamic mode's ceiling.</summary>
    public const uint DlssgDynamicMultiFrameCountMax = 0x10562D0F;

    /// <summary><c>NGX_DLSSG_DYNAMIC_TARGET_FRAME_RATE</c>: 0 disabled, 1–0xFFFFFF frames per second, <see cref="DynamicTargetAuto"/>.</summary>
    public const uint DlssgDynamicTargetFrameRate = 0x10CF4125;

    /// <summary><c>NGX_DLSS_OVERRIDE_OPTIMAL_SETTINGS</c>: 0 none, 1 <c>PERF_TO_9X</c>.</summary>
    public const uint DlssOptimalSettingsOverride = 0x10AFB76C;

    /// <summary>NVIDIA Smooth Motion, 0 off / 1 on — NOT in NVIDIA's header: documented by community profile tools only.</summary>
    public const uint SmoothMotion = 0xB0D384C0;

    /// <summary>The presets' "latest" value (<c>RENDER_PRESET_Latest</c>).</summary>
    public const uint PresetLatest = 0x00FF_FFFF;

    /// <summary>The frame-generation preset's "default" value (<c>RENDER_PRESET_Default</c>).</summary>
    public const uint PresetDefault = 0x00FF_FFFE;

    /// <summary><c>NGX_DLSSG_DYNAMIC_TARGET_FRAME_RATE_AUTO</c>.</summary>
    public const uint DynamicTargetAuto = 0x0100_0000;

    /// <summary>What <see cref="IDriverProfileSource"/> asks for, in the order the summary names them.</summary>
    public static IReadOnlyList<uint> All { get; } =
    [
        DlssSrOverride, DlssSrPreset, DlssSrMode, DlssSrScalingRatio, DlaaOverride,
        DlssRrOverride, DlssRrPreset, DlssRrMode, DlssRrScalingRatio,
        DlssFgOverride, DlssFgPreset, DlssgMultiFrameCount, DlssgMode, DlssgDynamicMultiFrameCountMax, DlssgDynamicTargetFrameRate,
        StreamlineDlssOverride, DlssOptimalSettingsOverride, SmoothMotion,
    ];
}

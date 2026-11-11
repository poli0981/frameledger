// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// The words <see cref="NvapiAdvancedReader"/> stores for the App to translate (<c>AdvancedValue.Tokens</c>,
/// <c>AdvancedRow.Token</c>). On disk once a release stores them; never renamed.
/// </summary>
public static class NvapiAdvancedTokens
{
    public const string Integrated = "integrated";
    public const string Discrete = "discrete";
    public const string Laptop = "laptop";
    public const string Desktop = "desktop";
    public const string Pci = "pci";
    public const string Agp = "agp";
    public const string PciExpress = "pci_express";
    public const string Fpci = "fpci";
    public const string Axi = "axi";
    public const string GameReady = "game_ready";
    public const string Studio = "studio";
    public const string RtxProduction = "rtx_production";
    public const string RtxNewFeature = "rtx_new_feature";
    public const string Hdr10 = "hdr10";
    public const string Hdr10Plus = "hdr10_plus";
    public const string Hdr10PlusGaming = "hdr10_plus_gaming";
    public const string DolbyVision = "dolby_vision";
    public const string Edr = "edr";
    public const string NotSupported = "not_supported";
    public const string VariableRefresh = "variable_refresh";
    public const string GsyncModule = "gsync_module";
    public const string AdaptiveSync = "adaptive_sync";
    public const string Ulmb = "ulmb";
    public const string UsableNow = "usable_now";
    public const string Srgb = "srgb";
    public const string ScRgbLinear = "scrgb_linear";
    public const string Bt2100Pq = "bt2100_pq";
    public const string DriverDefault = "driver_default";
    public const string UserSet = "user_set";
    public const string NotANumber = "not_a_number";
    public const string NoProfileForGame = "no_profile_for_game";

    /// <summary>Every token above, for the App's test that each has a translated string.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Integrated, Discrete, Laptop, Desktop, Pci, Agp, PciExpress, Fpci, Axi, GameReady, Studio, RtxProduction,
        RtxNewFeature, Hdr10, Hdr10Plus, Hdr10PlusGaming, DolbyVision, Edr, NotSupported, VariableRefresh, GsyncModule,
        AdaptiveSync, Ulmb, UsableNow, Srgb, ScRgbLinear, Bt2100Pq, DriverDefault, UserSet, NotANumber, NoProfileForGame,
    ];
}

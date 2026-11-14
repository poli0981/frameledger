// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Domain.Input;

namespace FrameLedger.Application.Input;

/// <summary>
/// The words a ledger stores for a controller's family, its connection and its battery (<c>06_DATA_MODEL</c>
/// §<c>sessions.input_devices</c>). They are data, not text shown to anyone: the App has its own, translated, and a
/// word here is never renamed once a release has written it.
/// </summary>
public static class InputNames
{
    public const string Other = "other";

    public const string Unknown = "unknown";

    public const string None = "none";

    public static string Of(ControllerFamily family) => family switch
    {
        ControllerFamily.Xbox => "xbox",
        ControllerFamily.PlayStation => "playstation",
        ControllerFamily.Nintendo => "nintendo",
        _ => Other,
    };

    public static ControllerFamily FamilyOf(string? name) => name switch
    {
        "xbox" => ControllerFamily.Xbox,
        "playstation" => ControllerFamily.PlayStation,
        "nintendo" => ControllerFamily.Nintendo,
        _ => ControllerFamily.Other,
    };

    public static string Of(ControllerConnection connection) => connection switch
    {
        ControllerConnection.Usb => "usb",
        ControllerConnection.Bluetooth => "bluetooth",
        ControllerConnection.Wireless => "wireless",
        _ => Unknown,
    };

    public static ControllerConnection ConnectionOf(string? name) => name switch
    {
        "usb" => ControllerConnection.Usb,
        "bluetooth" => ControllerConnection.Bluetooth,
        "wireless" => ControllerConnection.Wireless,
        _ => ControllerConnection.Unknown,
    };

    public static string Of(BatteryState state) => state switch
    {
        BatteryState.Discharging => "discharging",
        BatteryState.Charging => "charging",
        BatteryState.Full => "full",
        BatteryState.Wired => "wired",
        _ => Unknown,
    };

    public static BatteryState StateOf(string? name) => name switch
    {
        "discharging" => BatteryState.Discharging,
        "charging" => BatteryState.Charging,
        "full" => BatteryState.Full,
        "wired" => BatteryState.Wired,
        _ => BatteryState.Unknown,
    };

    public static string Of(BatteryScale scale) => scale switch
    {
        BatteryScale.FourLevels => "levels",
        BatteryScale.Percent => "percent",
        _ => None,
    };

    public static BatteryScale ScaleOf(string? name) => name switch
    {
        "levels" => BatteryScale.FourLevels,
        "percent" => BatteryScale.Percent,
        _ => BatteryScale.None,
    };
}

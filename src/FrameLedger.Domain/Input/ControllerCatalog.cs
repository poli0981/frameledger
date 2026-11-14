// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Input;

/// <summary>
/// What a controller's USB vendor and product ids say about it (<c>21_INPUT_DEVICES</c> §Who a controller is): its
/// family, a model name for the ones this build knows, and whether it is a pad a program presents rather than one a
/// player holds. The ids are the only thing read of a device's identity - never a serial number, never an address.
/// </summary>
/// <remarks>
/// The model names are product names, the same in every language. A product id that is not listed has no name, and the
/// App then shows the family and the two ids: a wrong name would be worse than none.
/// </remarks>
public static class ControllerCatalog
{
    public const int Microsoft = 0x045E;

    public const int Sony = 0x054C;

    public const int Nintendo = 0x057E;

    public const int Valve = 0x28DE;

    /// <summary>The Xbox 360 pad the Steam client presents when Steam Input stands between a controller and a game.</summary>
    public const int SteamVirtualGamepad = 0x11FF;

    public const int DualSense = 0x0CE6;

    public const int DualSenseEdge = 0x0DF2;

    private static readonly Dictionary<(int Vendor, int Product), string> _models = new()
    {
        [(Microsoft, 0x028E)] = "Xbox 360 Controller",
        [(Microsoft, 0x028F)] = "Xbox 360 Wireless Controller",
        [(Microsoft, 0x02D1)] = "Xbox One Controller",
        [(Microsoft, 0x02DD)] = "Xbox One Controller",
        [(Microsoft, 0x02E3)] = "Xbox Elite Wireless Controller",
        [(Microsoft, 0x02EA)] = "Xbox One S Controller",
        [(Microsoft, 0x02FD)] = "Xbox One S Controller",
        [(Microsoft, 0x0B00)] = "Xbox Elite Wireless Controller Series 2",
        [(Microsoft, 0x0B05)] = "Xbox Elite Wireless Controller Series 2",
        [(Microsoft, 0x0B22)] = "Xbox Elite Wireless Controller Series 2",
        [(Microsoft, 0x0B12)] = "Xbox Wireless Controller",
        [(Microsoft, 0x0B13)] = "Xbox Wireless Controller",
        [(Microsoft, 0x0B20)] = "Xbox Wireless Controller",
        [(Sony, 0x05C4)] = "DualShock 4",
        [(Sony, 0x09CC)] = "DualShock 4",
        [(Sony, DualSense)] = "DualSense",
        [(Sony, DualSenseEdge)] = "DualSense Edge",
        [(Nintendo, 0x2009)] = "Switch Pro Controller",
        [(Valve, SteamVirtualGamepad)] = "Steam Virtual Gamepad",
    };

    /// <summary>The family of a device with this vendor id; a device Windows presents through XInput is <see cref="ControllerFamily.Xbox"/> whoever made it.</summary>
    public static ControllerFamily FamilyOf(int vendorId, bool xinput) => vendorId switch
    {
        Microsoft => ControllerFamily.Xbox,
        Sony => ControllerFamily.PlayStation,
        Nintendo => ControllerFamily.Nintendo,
        _ => xinput ? ControllerFamily.Xbox : ControllerFamily.Other,
    };

    /// <summary>The model's product name, or null for ids this build does not list.</summary>
    public static string? ModelOf(int vendorId, int productId) => _models.GetValueOrDefault((vendorId, productId));

    /// <summary>Whether the ids are those of a pad a program presents - Steam's - rather than one a player holds.</summary>
    public static bool IsVirtual(int vendorId, int productId) => vendorId == Valve && productId == SteamVirtualGamepad;

    /// <summary>Whether the ids are a DualSense's or a DualSense Edge's, whose reports <see cref="DualSenseReport"/> reads.</summary>
    public static bool IsDualSense(int vendorId, int productId) => vendorId == Sony && productId is DualSense or DualSenseEdge;
}

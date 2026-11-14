// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Domain.Input;

namespace FrameLedger.Application.Input;

/// <summary>
/// One game controller the Agent saw, as far as a session keeps it (<c>21_INPUT_DEVICES</c> §Who a controller is): its
/// maker and model by their USB ids, how it is connected, and whether it is one of XInput's four pads.
/// </summary>
/// <remarks>
/// <b>What is not here is not read:</b> no serial number, no Bluetooth address, no device path. Two pads of one model
/// on one kind of connection cannot be told apart across a reconnect, and are not.
/// </remarks>
public sealed record InputDevice
{
    /// <summary>The reader's own number for the device, the same for as long as the reader runs. It names nothing outside the Agent.</summary>
    public required int Id { get; init; }

    public ControllerFamily Family { get; init; }

    /// <summary>The USB vendor id; 0 for one of XInput's pads that nothing could name.</summary>
    public int VendorId { get; init; }

    /// <summary>The USB product id; 0 with a <see cref="VendorId"/> of 0.</summary>
    public int ProductId { get; init; }

    public ControllerConnection Connection { get; init; }

    /// <summary>Whether the ids are those of a pad a program presents - Steam's virtual gamepad - rather than one a player holds.</summary>
    public bool IsVirtual { get; init; }

    /// <summary>Which of XInput's four pads this is (0-3); null for a controller Windows does not present through XInput.</summary>
    public int? XInputSlot { get; init; }

    /// <summary>The model's product name, or null for ids this build does not list.</summary>
    public string? Model => ControllerCatalog.ModelOf(VendorId, ProductId);
}

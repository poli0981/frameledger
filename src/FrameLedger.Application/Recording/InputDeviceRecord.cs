// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Input;

namespace FrameLedger.Application.Recording;

/// <summary>One controller of a session, in <see cref="InputDevicesRecord"/>.</summary>
public sealed record InputDeviceRecord
{
    /// <summary>The controller's number in the session, from 1: what its series are named by (<c>input.dev.N.active</c>).</summary>
    public int Index { get; init; }

    public string Family { get; init; } = InputNames.Other;

    public int VendorId { get; init; }

    public int ProductId { get; init; }

    public string? Model { get; init; }

    public string Connection { get; init; } = InputNames.Unknown;

    public int? XInputSlot { get; init; }

    /// <summary>The pad is one a program presents and no controller a player holds was found behind it.</summary>
    public bool Virtual { get; init; }

    /// <summary>A second pad moved exactly as this one did and was folded into it: Steam Input, or another remapper, was presenting this controller to games as an Xbox pad.</summary>
    public bool Remapped { get; init; }

    public long ConnectedMs { get; init; }

    /// <summary>The time it was in use while the game was in front.</summary>
    public long ActiveMs { get; init; }

    public int Connects { get; init; }

    public int Disconnects { get; init; }

    public InputBatteryRecord? Battery { get; init; }
}

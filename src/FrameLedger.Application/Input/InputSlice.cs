// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Input;

/// <summary>
/// What the input reader saw of the whole PC in one slice - about a tenth of a second (<c>21_INPUT_DEVICES</c> §The
/// reader). It is the same for every session running: which session a slice counts for is decided by whose window was
/// in front (<see cref="InputTickAccumulator"/>).
/// </summary>
public sealed record InputSlice
{
    /// <summary>The time since the slice before.</summary>
    public int ElapsedMs { get; init; }

    /// <summary>The process whose window was in front; 0 when Windows named none.</summary>
    public uint ForegroundProcessId { get; init; }

    /// <summary>Whether Windows' time of the last keyboard or mouse input moved during the slice.</summary>
    public bool KeyboardMouseActive { get; init; }

    /// <summary>Whether a pad a program presents was among XInput's pads.</summary>
    public bool VirtualPad { get; init; }

    /// <summary>The controllers connected during the slice.</summary>
    public IReadOnlyList<InputSliceDevice> Devices { get; init; } = [];

    /// <summary>What the reader's sources did since the reader started.</summary>
    public InputHealth Health { get; init; } = new();
}

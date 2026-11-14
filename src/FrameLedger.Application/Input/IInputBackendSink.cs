// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Input;

/// <summary>What <see cref="IInputBackend.Drain"/> hands over. A device is named by the handle Windows gave it for as long as it is connected: a key, never stored.</summary>
public interface IInputBackendSink
{
    /// <summary>A game controller is there: at the start, or newly connected.</summary>
    void Arrived(nint device, RawController controller);

    /// <summary>It left.</summary>
    void Removed(nint device);

    /// <summary>One input report of a controller, its report id first, at the length Windows hands the device's reports over.</summary>
    void Report(nint device, ReadOnlySpan<byte> report);
}

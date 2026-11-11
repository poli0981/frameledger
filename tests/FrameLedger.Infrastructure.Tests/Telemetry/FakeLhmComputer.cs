// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Infrastructure.Telemetry;
using LibreHardwareMonitor.Hardware;

namespace FrameLedger.Infrastructure.Tests.Telemetry;

/// <summary>A scripted <see cref="ILhmComputer"/>: the tree it lists, and whether opening or closing throws.</summary>
internal sealed class FakeLhmComputer(params IHardware[] hardware) : ILhmComputer
{
    public bool OpenThrows { get; set; }

    public bool CloseThrows { get; set; }

    public int Opens { get; private set; }

    public int Closes { get; private set; }

    public IReadOnlyList<IHardware> Hardware { get; } = hardware;

    public void Open()
    {
        Opens++;
        if (OpenThrows)
        {
            throw new InvalidOperationException("the driver refused");
        }
    }

    public void Update() => throw new NotSupportedException("the advanced reader refreshes device by device, never the whole tree");

    public void Close()
    {
        Closes++;
        if (CloseThrows)
        {
            throw new InvalidOperationException("already closed");
        }
    }
}

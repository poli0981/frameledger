// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Input;

namespace FrameLedger.Testing;

/// <summary>Windows, as the input reader asks it, with every answer written by the test: <b>no device is behind it</b>.</summary>
internal sealed class FakeInputBackend : IInputBackend
{
    private readonly List<Action<IInputBackendSink>> _pending = [];

    public bool Registers { get; set; } = true;

    public bool XInputAvailable { get; set; } = true;

    /// <summary>The pads XInput has, by slot.</summary>
    public Dictionary<int, XInputReading> Pads { get; } = [];

    public Dictionary<int, XInputDetails> Details { get; } = [];

    /// <summary>Windows' time of the last keyboard or mouse input; null is "Windows did not give it".</summary>
    public uint? LastInputTick { get; set; } = 1000;

    public uint Foreground { get; set; } = 42;

    public long CpuMs { get; set; }

    public bool ThrowOnDrain { get; set; }

    /// <summary>How many times each slot was asked for its state.</summary>
    public int[] PadAsks { get; } = new int[InputReader.Slots];

    public int DetailAsks { get; private set; }

    public bool Opened { get; private set; }

    public bool Disposed { get; private set; }

    public void Arrive(nint device, RawController controller) => _pending.Add(sink => sink.Arrived(device, controller));

    public void Leave(nint device) => _pending.Add(sink => sink.Removed(device));

    public void Send(nint device, params byte[] report) => _pending.Add(sink => sink.Report(device, report));

    public bool Open()
    {
        Opened = true;
        return Registers;
    }

    public void Drain(IInputBackendSink sink)
    {
        if (ThrowOnDrain)
        {
            throw new InvalidOperationException("the drain failed");
        }

        foreach (Action<IInputBackendSink> step in _pending)
        {
            step(sink);
        }

        _pending.Clear();
    }

    public bool TryReadPad(int slot, out XInputReading pad)
    {
        PadAsks[slot]++;
        return Pads.TryGetValue(slot, out pad);
    }

    public bool TryReadPadDetails(int slot, out XInputDetails details)
    {
        DetailAsks++;
        return Details.TryGetValue(slot, out details);
    }

    public bool TryReadLastInputTick(out uint tick)
    {
        tick = LastInputTick ?? 0;
        return LastInputTick is not null;
    }

    public uint ForegroundProcessId() => Foreground;

    public long ThreadCpuMs() => CpuMs;

    public void Dispose() => Disposed = true;
}

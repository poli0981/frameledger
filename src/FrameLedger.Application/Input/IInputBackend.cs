// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Input;

/// <summary>
/// The Windows calls the input reader makes, behind one interface (<c>21_INPUT_DEVICES</c> §What is read): raw input
/// for game controllers, XInput, the time of the last keyboard or mouse input, the window in front. Everything here
/// reads; nothing writes to a device, and nothing asks about a keyboard or a mouse beyond that one time
/// (<c>tools/input-readonly-check.ps1</c> holds the calls behind it to a list).
/// </summary>
/// <remarks>Every member is called on the reader's own thread, the one that called <see cref="Open"/>.</remarks>
public interface IInputBackend : IDisposable
{
    /// <summary>Makes what the reader needs on the calling thread; returns whether Windows accepted the registration for game controllers' reports.</summary>
    bool Open();

    /// <summary>Hands over what arrived since the last call: the controllers that came and went, then their reports.</summary>
    void Drain(IInputBackendSink sink);

    /// <summary>Whether XInput's library is there to ask.</summary>
    bool XInputAvailable { get; }

    /// <summary>The state of one of XInput's four pads; false when it is not connected.</summary>
    bool TryReadPad(int slot, out XInputReading pad);

    /// <summary>Whether the pad is wireless, and its battery; false when XInput did not answer.</summary>
    bool TryReadPadDetails(int slot, out XInputDetails details);

    /// <summary>The time of the last keyboard or mouse input in the user's session, as Windows' tick count; false when Windows did not give it.</summary>
    bool TryReadLastInputTick(out uint tick);

    /// <summary>The process whose window is in front; 0 when there is none.</summary>
    uint ForegroundProcessId();

    /// <summary>The processor time the calling thread has used, in milliseconds.</summary>
    long ThreadCpuMs();
}

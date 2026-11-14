// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Application.Input;

/// <summary>
/// One of XInput's pads at one read: <c>XINPUT_STATE</c> as it came. The reader asks one thing of it - is this a pad
/// in use (<c>Domain.Input.XInputPad.IsActive</c>) - and whether its packet number moved; neither it nor anything
/// derived from a single button or axis is kept.
/// </summary>
/// <param name="Packet"><c>dwPacketNumber</c>: it moves whenever anything on the pad changed.</param>
/// <param name="Buttons"><c>wButtons</c>.</param>
/// <param name="LeftTrigger"><c>bLeftTrigger</c>, 0-255.</param>
/// <param name="RightTrigger"><c>bRightTrigger</c>, 0-255.</param>
/// <param name="ThumbLx"><c>sThumbLX</c>.</param>
/// <param name="ThumbLy"><c>sThumbLY</c>.</param>
/// <param name="ThumbRx"><c>sThumbRX</c>.</param>
/// <param name="ThumbRy"><c>sThumbRY</c>.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct XInputReading(uint Packet, int Buttons, int LeftTrigger, int RightTrigger, int ThumbLx, int ThumbLy, int ThumbRx, int ThumbRy);

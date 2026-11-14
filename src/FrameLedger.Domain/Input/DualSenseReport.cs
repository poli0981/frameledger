// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Input;

/// <summary>
/// What one input report of a DualSense says that a session keeps (<c>21_INPUT_DEVICES</c> §DualSense): whether
/// anything on the pad was in use, and its battery when the report carries one. The buttons and sticks are looked at to
/// answer the first and are not returned - nothing here can say which was pressed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three reports.</b> Over USB the pad sends report <c>0x01</c>, 64 bytes. Over Bluetooth it sends a short report
/// <c>0x01</c> of 10 bytes - sticks, buttons and triggers, no battery - until some program asks it for a feature report
/// or sends it an output report; from then until it reconnects it sends report <c>0x31</c>, 78 bytes, whose body is the
/// USB one moved by one byte. FrameLedger never asks: a game that reads the short report would lose its input (owner
/// decision, 2026-11-14). So over Bluetooth the battery is there only when Steam or the game switched the pad.
/// </para>
/// <para>
/// The layout is the one the pad's makers' drivers and the open descriptions of the protocol agree on; it was written
/// from those descriptions, and <b>no report from a real pad had passed through it when it was written</b>.
/// </para>
/// </remarks>
public static class DualSenseReport
{
    public const int UsbLength = 64;

    public const int BluetoothShortLength = 10;

    public const int BluetoothLength = 78;

    /// <summary>How far a stick must be from its centre (128) to count: a quarter of its travel, as XInput's own dead zone is.</summary>
    public const int StickDeadzone = 32;

    /// <summary>How far a trigger must be pulled (0-255) to count: XInput's threshold.</summary>
    public const int TriggerThreshold = 30;

    private const int _usbReportId = 0x01;
    private const int _bluetoothReportId = 0x31;
    private const int _hatNeutral = 8;

    // Offsets in the full report's body, which starts after the report id over USB and one byte later over Bluetooth.
    private const int _fullTriggers = 4;
    private const int _fullButtons = 7;
    private const int _fullStatus = 52;
    private const int _fullBodyLength = 53;

    /// <summary>Reads one report; false for anything that is not one of the three, which is then not counted as a report of this pad.</summary>
    /// <param name="report">The report as Windows hands it over, its id first.</param>
    /// <param name="bluetooth">
    /// Whether the pad is on Bluetooth. The length cannot say: Windows hands every report of a device over at the length
    /// of its longest, so the short Bluetooth report arrives padded to 78 bytes with the same id as the USB one.
    /// </param>
    /// <param name="active">Whether anything on the pad was in use.</param>
    /// <param name="battery">The battery, when the report carries it.</param>
    public static bool TryRead(ReadOnlySpan<byte> report, bool bluetooth, out bool active, out BatteryReading? battery)
    {
        active = false;
        battery = null;
        if (report.IsEmpty)
        {
            return false;
        }

        if (!bluetooth)
        {
            if (report.Length < 1 + _fullBodyLength || report[0] != _usbReportId)
            {
                return false;
            }

            ReadFull(report[1..], out active, out battery);
            return true;
        }

        if (report[0] == _bluetoothReportId && report.Length >= 2 + _fullBodyLength)
        {
            ReadFull(report[2..], out active, out battery);
            return true;
        }

        if (report[0] == _usbReportId && report.Length >= BluetoothShortLength)
        {
            // x, y, rx, ry, three bytes of buttons (the third's upper six bits are a counter), the two triggers.
            active = SticksMoved(report[1..5]) || ButtonsDown(report[5], report[6], report[7] & 0x03) || TriggersPulled(report[8], report[9]);
            return true;
        }

        return false;
    }

    private static void ReadFull(ReadOnlySpan<byte> body, out bool active, out BatteryReading? battery)
    {
        // x, y, rx, ry, the two triggers, a counter, then the buttons: hat and face, shoulders and sticks, the rest.
        active = SticksMoved(body[..4])
            || TriggersPulled(body[_fullTriggers], body[_fullTriggers + 1])
            || ButtonsDown(body[_fullButtons], body[_fullButtons + 1], body[_fullButtons + 2]);
        battery = BatteryOf(body[_fullStatus]);
    }

    private static bool SticksMoved(ReadOnlySpan<byte> axes)
    {
        foreach (byte axis in axes)
        {
            if (Math.Abs(axis - 128) > StickDeadzone)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TriggersPulled(byte left, byte right) => left > TriggerThreshold || right > TriggerThreshold;

    private static bool ButtonsDown(byte hatAndFace, byte shoulders, int rest) =>
        (hatAndFace & 0x0F) < _hatNeutral || (hatAndFace & 0xF0) != 0 || shoulders != 0 || rest != 0;

    /// <summary>The status byte: the low four bits are the level in tenths (10 is 100 %), the high four what the battery is doing.</summary>
    private static BatteryReading BatteryOf(byte status)
    {
        int level = Math.Min(status & 0x0F, 10) * 10;
        return (status >> 4) switch
        {
            0x0 => new BatteryReading(BatteryState.Discharging, BatteryScale.Percent, level),
            0x1 => new BatteryReading(BatteryState.Charging, BatteryScale.Percent, level),
            0x2 => new BatteryReading(BatteryState.Full, BatteryScale.Percent, 100),

            // 0xA and 0xB are "not charging" for voltage or temperature, 0xF a charging fault: a level and no state.
            _ => new BatteryReading(BatteryState.Unknown, BatteryScale.Percent, level),
        };
    }
}

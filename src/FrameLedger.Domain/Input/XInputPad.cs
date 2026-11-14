// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Input;

/// <summary>
/// What XInput's answers about one of its four pads mean to a session (<c>21_INPUT_DEVICES</c> §XInput): whether the
/// state it returned is a pad in use, and what its battery words are. The thresholds are the ones XInput's own header
/// recommends, so "in use" here is what a game written to that header would call input.
/// </summary>
public static class XInputPad
{
    /// <summary><c>XINPUT_GAMEPAD_LEFT_THUMB_DEADZONE</c>.</summary>
    public const int LeftThumbDeadzone = 7849;

    /// <summary><c>XINPUT_GAMEPAD_RIGHT_THUMB_DEADZONE</c>.</summary>
    public const int RightThumbDeadzone = 8689;

    /// <summary><c>XINPUT_GAMEPAD_TRIGGER_THRESHOLD</c>.</summary>
    public const int TriggerThreshold = 30;

    private const int _typeWired = 0x01;
    private const int _typeAlkaline = 0x02;
    private const int _typeNimh = 0x03;
    private const int _levelFull = 0x03;

    /// <summary>Whether a state is a pad in use: a button down, a trigger past the threshold, or a stick outside its dead zone. A stick held counts; one that drifts inside the dead zone does not.</summary>
    public static bool IsActive(int buttons, int leftTrigger, int rightTrigger, int thumbLx, int thumbLy, int thumbRx, int thumbRy) =>
        buttons != 0
        || leftTrigger > TriggerThreshold
        || rightTrigger > TriggerThreshold
        || Outside(thumbLx, thumbLy, LeftThumbDeadzone)
        || Outside(thumbRx, thumbRy, RightThumbDeadzone);

    /// <summary>
    /// <c>XINPUT_BATTERY_INFORMATION</c> as a reading: null for "disconnected" and for a type this build does not know,
    /// <see cref="BatteryReading.Wired"/> for a wired pad, and the four levels for a battery. XInput does not say whether
    /// a battery charges, so one that is not wired is read as discharging.
    /// </summary>
    public static BatteryReading? Battery(int type, int level) => type switch
    {
        _typeWired => BatteryReading.Wired,
        _typeAlkaline or _typeNimh => new BatteryReading(BatteryState.Discharging, BatteryScale.FourLevels, Math.Clamp(level, 0, _levelFull)),

        // 0 is XInput's "disconnected" and 0xFF its "unknown": neither is a battery to show.
        _ => null,
    };

    private static bool Outside(int x, int y, int deadzone) => ((long)x * x) + ((long)y * y) > (long)deadzone * deadzone;
}

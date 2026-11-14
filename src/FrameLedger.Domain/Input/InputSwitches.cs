// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Input;

/// <summary>
/// How many times a session went from one thing to another (<c>03_METRICS</c> §Input): from a controller to the
/// keyboard and mouse and back, or from one maker's controller to another's.
/// </summary>
public static class InputSwitches
{
    /// <summary>
    /// The number of changes in a sequence of ticks, where each tick is either one value or nothing. A tick with
    /// nothing - no input, or two things at once - is skipped and does not end what came before it: a player who puts
    /// the controller down, waits, and picks it up again has not switched.
    /// </summary>
    public static int Count<T>(IEnumerable<T?> ticks)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(ticks);
        int switches = 0;
        T? previous = null;
        foreach (T? tick in ticks)
        {
            if (tick is not { } now)
            {
                continue;
            }

            if (previous is { } before && !EqualityComparer<T>.Default.Equals(before, now))
            {
                switches++;
            }

            previous = now;
        }

        return switches;
    }

    /// <summary>The one class a tick was played with, or null when it was played with both or with neither.</summary>
    public static InputClass? Exclusive(long controllerMs, long keyboardMouseMs, long bothMs)
    {
        if (bothMs > 0 || (controllerMs > 0 && keyboardMouseMs > 0))
        {
            return null;
        }

        if (controllerMs > 0)
        {
            return InputClass.Controller;
        }

        return keyboardMouseMs > 0 ? InputClass.KeyboardMouse : null;
    }

    /// <summary>The class a tick was mostly played with - what its point on the timeline shows - or null when the game was not in front at all.</summary>
    public static InputClass? Dominant(long controllerMs, long keyboardMouseMs, long bothMs, long idleMs)
    {
        if (controllerMs + keyboardMouseMs + bothMs + idleMs <= 0)
        {
            return null;
        }

        InputClass best = InputClass.None;
        long most = idleMs;
        if (keyboardMouseMs > most)
        {
            (best, most) = (InputClass.KeyboardMouse, keyboardMouseMs);
        }

        if (controllerMs > most)
        {
            (best, most) = (InputClass.Controller, controllerMs);
        }

        return bothMs > most ? InputClass.Both : best;
    }
}

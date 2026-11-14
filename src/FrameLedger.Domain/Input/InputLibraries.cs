// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Input;

/// <summary>
/// The libraries through which a Windows game reads controllers, by file name (owner decision D163,
/// <c>05_DETECTION</c> §Input libraries): the second of the three pieces of evidence for "supports controllers". A
/// hooked session's module snapshot notes which of them the game had loaded.
/// </summary>
/// <remarks>
/// <b>Loaded is not used.</b> Several engines load XInput whether the game reads a pad or not, so a library here says
/// how a game COULD read a controller - which is why it is shown as evidence, beside what the store lists and how the
/// game was played, and never as a verdict.
/// </remarks>
public static class InputLibraries
{
    private static readonly (string File, string Api)[] _known =
    [
        ("xinput1_4.dll", "XInput"),
        ("xinput1_3.dll", "XInput"),
        ("xinput9_1_0.dll", "XInput"),
        ("xinputuap.dll", "XInput"),
        ("dinput8.dll", "DirectInput"),
        ("dinput.dll", "DirectInput"),
        ("windows.gaming.input.dll", "Windows.Gaming.Input"),
        ("gameinput.dll", "GameInput"),
        ("sdl2.dll", "SDL"),
        ("sdl3.dll", "SDL"),
    ];

    /// <summary>The file names a module snapshot looks for, lower case.</summary>
    public static IReadOnlyList<string> FileNames { get; } = [.. _known.Select(static k => k.File)];

    /// <summary>The interface a file is - <c>XInput</c>, <c>DirectInput</c>, <c>Windows.Gaming.Input</c>, <c>GameInput</c>, <c>SDL</c> - or null when it is not an input library.</summary>
    public static string? ApiOf(string? fileName)
    {
        foreach ((string file, string api) in _known)
        {
            if (string.Equals(file, fileName, StringComparison.OrdinalIgnoreCase))
            {
                return api;
            }
        }

        return null;
    }

    /// <summary>Whether a file is an input library.</summary>
    public static bool Is(string? fileName) => ApiOf(fileName) is not null;
}

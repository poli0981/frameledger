// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Capture;

/// <summary>
/// A module path's folder class (beta.24, D64), and the NVIDIA override folder a snapshot reads by path rather than by
/// name. Pure: the two system folders are handed in, so the rules are tested without the machine's.
/// </summary>
public static class RuntimeModuleSources
{
    /// <summary>The class of <paramref name="path"/>: the driver store and <c>%ProgramData%\NVIDIA</c> first, then Windows, else the game.</summary>
    public static RuntimeModuleSource Classify(string? path, string windowsDirectory, string programDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(windowsDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(programDataDirectory);
        if (string.IsNullOrWhiteSpace(path))
        {
            return RuntimeModuleSource.Unknown;
        }

        if (Under(path, Path.Combine(windowsDirectory, "System32", "DriverStore")) || Under(path, Path.Combine(programDataDirectory, "NVIDIA")))
        {
            return RuntimeModuleSource.GraphicsDriver;
        }

        return Under(path, windowsDirectory) ? RuntimeModuleSource.Windows : RuntimeModuleSource.Game;
    }

    /// <summary>
    /// Whether <paramref name="path"/> is under <c>%ProgramData%\NVIDIA\NGX</c>, where the NVIDIA App's override keeps its
    /// DLSS libraries under names no census lists — so a snapshot records every module there, whatever its name.
    /// </summary>
    public static bool IsNgxOverrideFolder(string? path, string programDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(programDataDirectory);
        return !string.IsNullOrWhiteSpace(path) && Under(path, Path.Combine(programDataDirectory, "NVIDIA", "NGX"));
    }

    /// <summary>The stored token for a class; null for <see cref="RuntimeModuleSource.Unknown"/>.</summary>
    public static string? Token(RuntimeModuleSource source) => source switch
    {
        RuntimeModuleSource.Game => "game",
        RuntimeModuleSource.GraphicsDriver => "graphics_driver",
        RuntimeModuleSource.Windows => "windows",
        _ => null,
    };

    /// <summary>A stored token back; an unknown token is <see cref="RuntimeModuleSource.Unknown"/>.</summary>
    public static RuntimeModuleSource FromToken(string? token) => token switch
    {
        "game" => RuntimeModuleSource.Game,
        "graphics_driver" => RuntimeModuleSource.GraphicsDriver,
        "windows" => RuntimeModuleSource.Windows,
        _ => RuntimeModuleSource.Unknown,
    };

    private static bool Under(string path, string folder)
    {
        string full = Normalize(path);
        string root = Normalize(folder);
        return full.Length > root.Length
            && full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            && full[root.Length] == Path.DirectorySeparatorChar;
    }

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
}

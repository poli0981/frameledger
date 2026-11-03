// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Infrastructure.Import;

namespace FrameLedger.Infrastructure.Detection;

/// <summary>
/// Where a game's install actually begins, given the path of its executable.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Not the executable's own directory.</strong> Unreal puts the exe at
/// <c>&lt;root&gt;\&lt;Project&gt;\Binaries\Win64\</c>, so treating its parent as
/// "the game directory" misses everything at the install root — measured on
/// Lies of P, whose DLSS plugin lives at
/// <c>Engine\Plugins\Runtime\Nvidia\DLSS\Binaries\ThirdParty\Win64\</c> and was
/// reported as absent.
/// </para>
/// <para>
/// The boundaries are hardcoded rather than read from the rules feed, for the
/// same reason <c>IsPlatformLauncher</c> is: a data-driven boundary would let a
/// rules update move where the anti-cheat pre-scan looks, which is a way to
/// widen the guard's blind spot from a file nobody reviews as code.
/// </para>
/// <para>
/// <strong>When no boundary is recognised the exe's own directory is used.</strong>
/// Going up blindly is worse than staying put: one level above
/// <c>D:\another\epic\AlanWake2</c> is a folder of unrelated games, and scanning
/// a sibling title's anti-cheat would be a false refusal with no appeal.
/// </para>
/// <para>
/// <strong>Except at a GDK package root (beta.33).</strong> The Xbox app installs to
/// <c>&lt;games folder&gt;\&lt;Game&gt;\Content\</c> under a folder the user names, so no
/// string can bound it; the package marks its own root instead, with
/// <c>MicrosoftGame.config</c> beside <c>appxmanifest.xml</c>. Only when no string
/// boundary matched, the nearest folder at or above the executable's that holds
/// both is the root (<see cref="GdkPackageFiles.NearestPackageRoot"/>) — which can
/// only widen the walk relative to the exe's own directory, never narrow it. Without
/// it, an Unreal title's <c>Content\…\Binaries\WinGDK</c> was all check 4 saw
/// (<c>spike-notes</c> §25). The native <c>ResolveInstallRoot</c> answers the same,
/// and a shared case list (<c>tests/fixtures/install-roots/cases.txt</c>) holds the
/// two to it.
/// </para>
/// </remarks>
public static class InstallRoot
{
    /// <summary>
    /// Path segments whose <em>child</em> is a game's install root.
    /// </summary>
    /// <remarks>
    /// Matched case-insensitively against whole segments, so a folder merely
    /// called "steamapps" somewhere unrelated cannot masquerade as one.
    /// </remarks>
    private static readonly string[][] _boundaries =
    [
        ["steamapps", "common"],
        ["GOG Galaxy", "Games"],
        ["Epic Games"],
        ["GamesInstalled"],
    ];

    /// <summary>Resolves the install root for <paramref name="exePath"/>.</summary>
    /// <param name="exePath">Full path to the game executable.</param>
    /// <returns>The install root, or the executable's directory when no boundary is recognised.</returns>
    public static string Resolve(string exePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);

        string exeDir = Path.GetDirectoryName(Path.GetFullPath(exePath)) ?? string.Empty;
        if (exeDir.Length == 0)
        {
            return exeDir;
        }

        string[] segments = exeDir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (string[] boundary in _boundaries)
        {
            // Scan left to right and keep the FIRST match: a library nested
            // inside another library is pathological, and the outermost
            // boundary is the one that bounds the install.
            for (int i = 0; i + boundary.Length < segments.Length; i++)
            {
                bool hit = true;
                for (int b = 0; b < boundary.Length && hit; b++)
                {
                    hit = string.Equals(segments[i + b], boundary[b], StringComparison.OrdinalIgnoreCase);
                }

                if (hit)
                {
                    // The game's root is the single segment after the boundary.
                    return string.Join(Path.DirectorySeparatorChar, segments[..(i + boundary.Length + 1)]);
                }
            }
        }

        return GdkPackageFiles.NearestPackageRoot(exeDir) ?? exeDir;
    }
}

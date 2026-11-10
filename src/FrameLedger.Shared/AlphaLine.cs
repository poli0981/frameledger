// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Reflection;

namespace FrameLedger.Shared;

/// <summary>
/// Two facts an alpha build's own words turn on (owner decisions D97 and D98, 2026-11-11). An alpha is a beta's source
/// built to try a newer .NET, which is at times a prerelease and at times - 0.1.0-alpha.4 - the release the betas
/// moved to as well; so the build says "a prerelease .NET" only where the runtime it runs on is one
/// (<see cref="IsPrerelease"/>), and says that its line is paused where it was cut while the line rested
/// (<see cref="Rests"/>): a resting alpha gets no update, its anti-cheat list included, and tells its user to install
/// the beta (<c>docs/12_BUILD.md</c> §The alpha line).
/// </summary>
public static class AlphaLine
{
    /// <summary>The metadata key <c>Directory.Build.props</c> writes from its <c>FlAlphaLineRests</c> property.</summary>
    public const string RestsMetadataKey = "FrameLedger.AlphaLineRests";

    /// <summary>
    /// Whether the alpha line was resting when this build was made - the betas on the same .NET, no further alpha until a
    /// newer one is tried. Stamped on both lines and read only by an alpha; false for an assembly that carries no stamp.
    /// </summary>
    public static bool Rests { get; } = ParseRests(typeof(AlphaLine).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(static a => string.Equals(a.Key, RestsMetadataKey, StringComparison.Ordinal))?.Value);

    /// <summary>Only the word <c>true</c>, in any case, says the line rests.</summary>
    public static bool ParseRests(string? stamped) => string.Equals(stamped, "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a runtime's own description (<c>RuntimeInformation.FrameworkDescription</c>) names a prerelease:
    /// <c>.NET 11.0.0-rc.3.26522.148</c> does, <c>.NET 11.0.0</c> does not. The version is the last word, and a
    /// prerelease version is the one with a hyphen in it.
    /// </summary>
    public static bool IsPrerelease(string frameworkDescription)
    {
        ArgumentNullException.ThrowIfNull(frameworkDescription);
        ReadOnlySpan<char> version = frameworkDescription.AsSpan().TrimEnd();
        int space = version.LastIndexOf(' ');
        return version[(space + 1)..].Contains('-');
    }
}

// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Update;

/// <summary>
/// Which releases a check asks for (beta.14, owner decision D47). <c>update.channel</c> is <c>auto</c> unless the user chose:
/// a copy that is itself a pre-release looks for pre-releases and a release looks for releases. Until beta.14 the default was
/// <c>stable</c> while every release so far is a GitHub pre-release, so a copy left on its defaults never found an update —
/// the owner's own updater log read "No releases found" from the day the ledger was reset. A stored <c>stable</c> or
/// <c>beta</c> is a choice the user made, and it wins.
/// </summary>
public static class UpdateChannelPolicy
{
    public const string Automatic = "auto";

    public const string Stable = "stable";

    public const string Beta = "beta";

    /// <summary>Whether a version is a pre-release in SemVer's sense: a <c>-</c> before any build metadata (<c>0.1.0-beta.14</c>).</summary>
    public static bool IsPrerelease(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        int build = version.IndexOf('+', StringComparison.Ordinal);
        int dash = version.IndexOf('-', StringComparison.Ordinal);
        return dash > 0 && (build < 0 || dash < build);
    }

    /// <summary>Whether a check on <paramref name="channel"/> asks for pre-releases, for a copy running <paramref name="runningVersion"/>.</summary>
    public static bool IncludesPrereleases(string? channel, string? runningVersion) => channel switch
    {
        Beta => true,
        Stable => false,
        _ => IsPrerelease(runningVersion),
    };
}

// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FrameLedger.Shared;

namespace FrameLedger.App.Services;

/// <summary>
/// What an alpha build says about the .NET it runs on (owner decisions D97 and D98, 2026-11-11). Until 0.1.0-alpha.3 every
/// alpha ran on a prerelease and the texts said so unconditionally; 0.1.0-alpha.4 runs on .NET 11's release, the betas'
/// own from 0.1.0-beta.37, and its line rests after it. So the words follow two facts handed in here rather than read
/// here - a test gives each combination: the runtime's description, and whether the build was cut while the line rested.
/// </summary>
internal static class AlphaTexts
{
    /// <summary>
    /// The banner above every page of an alpha. A prerelease runtime is said first: it is what the process can see for
    /// itself, and a line that tries a prerelease is not resting whatever the build was stamped with.
    /// </summary>
    [SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture")]
    public static string BannerBody(bool rests, string runtime)
    {
        string format = AlphaLine.IsPrerelease(runtime) ? Strings.Alpha_Banner_Body_Prerelease_Format
            : rests ? Strings.Alpha_Banner_Body_Resting_Format
            : Strings.Alpha_Banner_Body_Format;
        return string.Format(CultureInfo.CurrentCulture, format, runtime);
    }

    /// <summary>About's line naming the runtime: a beta's, an alpha's, and an alpha's on a prerelease.</summary>
    [SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture")]
    public static string AboutRuntime(bool alpha, string runtime)
    {
        string format = !alpha ? Strings.About_Runtime_Format
            : AlphaLine.IsPrerelease(runtime) ? Strings.About_Runtime_Alpha_Prerelease_Format
            : Strings.About_Runtime_Alpha_Format;
        return string.Format(CultureInfo.CurrentCulture, format, runtime);
    }
}

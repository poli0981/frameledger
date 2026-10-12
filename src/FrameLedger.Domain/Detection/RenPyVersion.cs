// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.Text.RegularExpressions;

namespace FrameLedger.Domain.Detection;

/// <summary>The Ren'Py version a title was built with (beta.20), decided from <see cref="RenPyBuildFacts"/> — or nothing.</summary>
/// <remarks>
/// One witness: the engine's own <c>renpy/vc_version.py</c>. Its <c>version</c> carries a fourth number, the build
/// (<c>8.5.0.25111603</c>); a Ren'Py release is named by the first three (Ren'Py 8.5.0), so those are kept. A title whose
/// engine names no version there reads as none — nothing else is guessed from.
/// </remarks>
public static partial class RenPyVersion
{
    /// <summary>The version these facts establish, and the witness it rests on.</summary>
    public static EngineVersionReading Decide(RenPyBuildFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        return facts.VcVersion is { } text && Shape().Match(text.Trim()) is { Success: true } m
            && Parse(m.Groups["major"].Value) is >= 1 and <= 99 and int major
            && Parse(m.Groups["minor"].Value) is >= 0 and int minor
            && Parse(m.Groups["patch"].Value) is >= 0 and int patch
            ? new EngineVersionReading(string.Create(CultureInfo.InvariantCulture, $"{major}.{minor}.{patch}"), EngineVersionSource.RenPyVcVersion)
            : EngineVersionReading.None;
    }

    private static int Parse(string digits) =>
        int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int value) ? value : -1;

    [GeneratedRegex(@"^(?<major>\d{1,2})\.(?<minor>\d{1,3})\.(?<patch>\d{1,3})(?:\.\d{1,12})?$", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex Shape();
}

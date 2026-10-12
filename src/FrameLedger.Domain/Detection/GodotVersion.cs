// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.Text.RegularExpressions;

namespace FrameLedger.Domain.Detection;

/// <summary>The Godot version a title was exported with (beta.19), decided from <see cref="GodotBuildFacts"/> — or nothing.</summary>
/// <remarks>
/// <para>
/// <b>The ladder:</b> (1) a data pack's header, beside the executable or appended to it — the engine writes its own
/// version there; (2) the executable's version resource, only while its <c>ProductName</c> is still <c>Godot Engine</c>:
/// an export whose project set no version information keeps the engine's (<c>4.2.2.stable.official</c> on Plethora of
/// Bullets 2), and one that set its own carries the game's (衔蝶: <c>0.1.0.3</c>), which is not the engine's.
/// </para>
/// <para>
/// A zero patch is not written, because Godot does not write it: 4.3, 4.2.2.
/// </para>
/// </remarks>
public static partial class GodotVersion
{
    /// <summary>The product name a Godot export keeps when its project set none.</summary>
    public const string EngineProductName = "Godot Engine";

    /// <summary>The version these facts establish, and the witness it rests on.</summary>
    public static EngineVersionReading Decide(GodotBuildFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (facts.PackVersion is { Count: >= 3 } pack && Format(pack[0], pack[1], pack[2]) is { } fromPack)
        {
            return new EngineVersionReading(fromPack, facts.PackEmbedded ? EngineVersionSource.GodotPackEmbedded : EngineVersionSource.GodotPack);
        }

        if (string.Equals(facts.ExeProductName?.Trim(), EngineProductName, StringComparison.OrdinalIgnoreCase)
            && facts.ExeProductVersion is { } text && ProductShape().Match(text.Trim()) is { Success: true } m
            && Format(Parse(m.Groups["major"].Value), Parse(m.Groups["minor"].Value), m.Groups["patch"].Success ? Parse(m.Groups["patch"].Value) : 0) is { } fromExe)
        {
            return new EngineVersionReading(fromExe, EngineVersionSource.GodotExe);
        }

        return EngineVersionReading.None;
    }

    /// <summary><c>4.2.2</c>, <c>4.3</c> — or null for numbers no Godot release carries (a pack written by something else).</summary>
    private static string? Format(int major, int minor, int patch) =>
        major is < 1 or > 9 || minor is < 0 or > 99 || patch is < 0 or > 99
            ? null
            : patch == 0
                ? string.Create(CultureInfo.InvariantCulture, $"{major}.{minor}")
                : string.Create(CultureInfo.InvariantCulture, $"{major}.{minor}.{patch}");

    private static int Parse(string digits) =>
        int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int value) ? value : -1;

    [GeneratedRegex(@"^(?<major>\d{1,2})\.(?<minor>\d{1,2})(?:\.(?<patch>\d{1,2}))?(?:\.[a-z]|$)", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex ProductShape();
}

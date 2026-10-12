// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.Text.RegularExpressions;

namespace FrameLedger.Domain.Detection;

/// <summary>
/// The Unity version a title was built with (beta.19, owner request: "detect the version of Unity, Godot and RPG Maker
/// games"), decided from <see cref="UnityBuildFacts"/> — or nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>The ladder, strongest first</b> (measured on fifteen installs 2026-10-12):
/// </para>
/// <list type="number">
/// <item>The player library's <c>ProductVersion</c> is an editor version — <c>2022.3.39f1</c>, the release letter and its
/// number included (thirteen of fifteen).</item>
/// <item>The data file's header names one (A Space for the Unbound: <c>2019.4.1f1</c>, beside a player that carries only
/// numbers).</item>
/// <item>The player's numeric file version: MAJOR.MINOR.PATCH and no release letter (GIRLS' FRONTLINE 2: <c>2019.4.40</c>,
/// whose data file is encrypted). The player library is Unity's own binary — a studio does not number it — so this rung
/// is the engine's, only shorter.</item>
/// </list>
/// <para>
/// Until beta.19 the rules read the player's <c>FileVersion</c> string as it is (<c>2022.3.39.5118735</c>, the build
/// number where the release letter belongs).
/// </para>
/// </remarks>
public static partial class UnityVersion
{
    /// <summary>The version these facts establish, and the rung it stands on.</summary>
    public static EngineVersionReading Decide(UnityBuildFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (EditorVersion(facts.PlayerProductVersion) is { } product)
        {
            return new EngineVersionReading(product, EngineVersionSource.UnityPlayer);
        }

        if (EditorVersion(facts.DataHeaderVersion) is { } header)
        {
            return new EngineVersionReading(header, EngineVersionSource.UnityData);
        }

        return facts.PlayerFileVersion is { Count: >= 3 } parts && IsUnityMajor(parts[0]) && parts[1] is >= 0 and <= 99 && parts[2] is >= 0 and <= 999
            ? new EngineVersionReading(string.Create(CultureInfo.InvariantCulture, $"{parts[0]}.{parts[1]}.{parts[2]}"), EngineVersionSource.UnityPlayerFile)
            : EngineVersionReading.None;
    }

    /// <summary>
    /// The editor version a text starts with — <c>2022.3.39f1</c> of <c>2022.3.39f1 (4e1b0f82c39a)</c>, <c>6000.0.58f2</c> —
    /// or null when it is not one: a major Unity never used, or no release letter (<c>2019.4.1.15122501</c>).
    /// </summary>
    public static string? EditorVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        Match m = EditorShape().Match(text.Trim());
        return m.Success && int.TryParse(m.Groups["major"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int major) && IsUnityMajor(major)
            ? m.Groups["version"].Value
            : null;
    }

    /// <summary>
    /// The majors Unity has shipped: 3 to 5, the years 2017 to 2023, and Unity 6's <c>6000</c> line (6000.0 is Unity 6,
    /// 6000.3 is 6.3).
    /// </summary>
    private static bool IsUnityMajor(int major) => major is >= 3 and <= 5 or >= 2017 and <= 2023 or >= 6000 and <= 6999;

    [GeneratedRegex(@"^(?<version>(?<major>\d{1,4})\.\d{1,2}\.\d{1,3}[abfpx]\d{1,3})(?![\d.])", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex EditorShape();
}

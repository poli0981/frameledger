// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.Text.RegularExpressions;

namespace FrameLedger.Domain.Detection;

/// <summary>
/// Which RPG Maker built a title, and at which version (beta.19), decided from <see cref="RpgMakerBuildFacts"/> — or nothing.
/// The witness names the maker: the App shows <c>rpgmaker_mv</c> as "RPG Maker MZ" when the version came from MZ's core
/// script, and <c>rpgmaker_rgss</c> as "RPG Maker VX Ace" when it came from an RGSS3 library.
/// </summary>
public static partial class RpgMakerVersion
{
    /// <summary>MZ's core script.</summary>
    public const string MzCoreScript = "rmmz_core.js";

    /// <summary>MV's core script.</summary>
    public const string MvCoreScript = "rpg_core.js";

    /// <summary>The version these facts establish, and the witness it rests on.</summary>
    public static EngineVersionReading Decide(RpgMakerBuildFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (ScriptVersion(facts.CoreScriptVersion) is { } script)
        {
            if (string.Equals(facts.CoreScript, MzCoreScript, StringComparison.OrdinalIgnoreCase))
            {
                return new EngineVersionReading(script, EngineVersionSource.RpgMakerMzCore);
            }

            if (string.Equals(facts.CoreScript, MvCoreScript, StringComparison.OrdinalIgnoreCase))
            {
                return new EngineVersionReading(script, EngineVersionSource.RpgMakerMvCore);
            }
        }

        if (facts.RgssLibrary is null || LibraryShape().Match(facts.RgssLibrary.Trim()) is not { Success: true } m)
        {
            return EngineVersionReading.None;
        }

        int generation = m.Groups["generation"].Value[0] - '0';
        string source = generation switch
        {
            1 => EngineVersionSource.Rgss1Library,
            2 => EngineVersionSource.Rgss2Library,
            _ => EngineVersionSource.Rgss3Library,
        };

        // The library's own numbers when they are its generation's; else the ones its name carries (RGSS301 → 3.0.1).
        string version = facts.RgssFileVersion is { Count: >= 3 } parts && parts[0] == generation && parts[1] is >= 0 and <= 9 && parts[2] is >= 0 and <= 9
            ? string.Create(CultureInfo.InvariantCulture, $"{parts[0]}.{parts[1]}.{parts[2]}")
            : string.Create(CultureInfo.InvariantCulture, $"{generation}.{m.Groups["minor"].Value}.{m.Groups["patch"].Value}");
        return new EngineVersionReading(version, source);
    }

    /// <summary>A core script's version as its header writes it (<c>1.10.0</c>), or null when it does not read as one.</summary>
    public static string? ScriptVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        Match m = ScriptShape().Match(text.Trim());
        return m.Success ? m.Value : null;
    }

    [GeneratedRegex(@"^\d{1,2}\.\d{1,3}\.\d{1,3}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex ScriptShape();

    [GeneratedRegex(@"^RGSS(?<generation>[123])(?<minor>\d)(?<patch>\d)[A-Z]?\.dll$", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 250)]
    private static partial Regex LibraryShape();
}

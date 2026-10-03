// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.Text.RegularExpressions;

namespace FrameLedger.Domain.Detection;

/// <summary>
/// The Unreal Engine version an executable was built with (beta.10, owner request: "detect the exact Unreal Engine version"),
/// decided from <see cref="UnrealBuildFacts"/> — or nothing, which the product says rather than guesses.
/// </summary>
/// <remarks>
/// <para>
/// <b>The ladder, strongest first</b> (measured on ten titles 2026-09-27, <c>05_DETECTION</c> §Engine version):
/// </para>
/// <list type="number">
/// <item>The branch name and the numeric file version agree on MAJOR.MINOR: the version is the file's MAJOR.MINOR.PATCH
/// (Martha Is Dead 4.27.0, UMIGARI 5.5.4, AbyssMemory 4.26.1, Black Myth: Wukong 5.0.0).</item>
/// <item>A branch name alone: MAJOR.MINOR and no patch — the studio replaced the file version with its own (SILENT HILL 2:
/// 1.0.0.5 beside <c>++UE5+Release-5.1</c> → 5.1).</item>
/// <item>No branch name, and a file version that reads as an engine's (4.x up to 4.27, 5.x up to 5.9) where nothing says the
/// studio replaced it — no version strings at all, a string naming the same <c>UE4</c>/<c>UE5</c>, or Epic's company name
/// (Lies of P 4.27.2, Expedition 33 5.4.4, Hell Is Us 5.5.4, whose studio renamed its branch).</item>
/// <item>Anything else is no version (Cronos: 1.2.0.0 and no branch name; Rune Factory: 1.0.3.0).</item>
/// </list>
/// <para>
/// The third rung is the weakest: a studio could number its own game 4.2 and ship no version strings. The App therefore
/// says which rung a version stands on, and a version on it is never presented as more than "the executable's version
/// number" (<c>LIMITATIONS.md</c>).
/// </para>
/// </remarks>
public static partial class UnrealVersion
{
    /// <summary>The last Unreal Engine 4 release line (4.27).</summary>
    public const int MaxUe4Minor = 27;

    /// <summary>Headroom for Unreal Engine 5's release lines; 5.10 would read as not an engine version until this moves.</summary>
    public const int MaxUe5Minor = 9;

    /// <summary>A patch number no engine release has come near; anything larger is a studio's numbering.</summary>
    public const int MaxPatch = 20;

    /// <summary>The company name an engine build carries when the project never set its own.</summary>
    public const string EpicCompanyName = "Epic Games, Inc.";

    /// <summary>The version these facts establish, and the rung it stands on.</summary>
    public static EngineVersionReading Decide(UnrealBuildFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        SortedSet<string> lines = ReleaseLines(facts);
        (int Major, int Minor, int Patch)? file = PlausibleFileVersion(facts.FixedFileVersion);

        if (lines.Count > 0)
        {
            if (file is { } f && lines.Contains(Line(f.Major, f.Minor)))
            {
                return new EngineVersionReading(Full(f), EngineVersionSource.UnrealBranchAndFile);
            }

            // Two release lines and no file version to choose between them: saying either would be a guess.
            return lines.Count == 1 ? new EngineVersionReading(lines.Min, EngineVersionSource.UnrealBranch) : EngineVersionReading.None;
        }

        return file is { } g && NothingReplacedIt(facts, g.Major)
            ? new EngineVersionReading(Full(g), EngineVersionSource.UnrealFileVersion)
            : EngineVersionReading.None;
    }

    /// <summary>MAJOR.MINOR of a branch name such as <c>5.1</c>, or null when it does not read as an Unreal 4/5 release line.</summary>
    public static string? ReleaseLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        Match m = LineShape().Match(text.Trim());
        return m.Success && Parse(m.Groups["major"].Value) is { } major && Parse(m.Groups["minor"].Value) is { } minor && IsEngineLine(major, minor)
            ? Line(major, minor)
            : null;
    }

    private static SortedSet<string> ReleaseLines(UnrealBuildFacts facts)
    {
        var lines = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string branch in facts.BranchVersions)
        {
            if (ReleaseLine(branch) is { } line)
            {
                lines.Add(line);
            }
        }

        // A project that kept the engine's version strings carries the branch name in them too
        // ("++UE5+Release-5.5-CL-40574608"): the same witness, read without touching the file again.
        foreach (string? text in new[] { facts.FileVersionText, facts.ProductVersionText })
        {
            if (text is null)
            {
                continue;
            }

            foreach (Match m in BranchName().Matches(text))
            {
                if (ReleaseLine(m.Groups["line"].Value) is { } line)
                {
                    lines.Add(line);
                }
            }
        }

        return lines;
    }

    private static (int Major, int Minor, int Patch)? PlausibleFileVersion(IReadOnlyList<int>? parts)
    {
        if (parts is not { Count: >= 3 } || parts[2] < 0 || parts[2] > MaxPatch || !IsEngineLine(parts[0], parts[1]))
        {
            return null;
        }

        return (parts[0], parts[1], parts[2]);
    }

    private static bool NothingReplacedIt(UnrealBuildFacts facts, int major)
    {
        if (string.IsNullOrWhiteSpace(facts.FileVersionText) && string.IsNullOrWhiteSpace(facts.ProductVersionText))
        {
            return true;
        }

        string engine = "UE" + major.ToString(CultureInfo.InvariantCulture);
        return (facts.FileVersionText?.Contains(engine, StringComparison.Ordinal) ?? false)
            || (facts.ProductVersionText?.Contains(engine, StringComparison.Ordinal) ?? false)
            || string.Equals(facts.CompanyName?.Trim(), EpicCompanyName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEngineLine(int major, int minor) => major switch
    {
        4 => minor is >= 0 and <= MaxUe4Minor,
        5 => minor is >= 0 and <= MaxUe5Minor,
        _ => false,
    };

    private static int? Parse(string digits) =>
        int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int value) ? value : null;

    private static string Line(int major, int minor) => string.Create(CultureInfo.InvariantCulture, $"{major}.{minor}");

    private static string Full((int Major, int Minor, int Patch) v) =>
        string.Create(CultureInfo.InvariantCulture, $"{v.Major}.{v.Minor}.{v.Patch}");

    [GeneratedRegex(@"^(?<major>\d{1,2})\.(?<minor>\d{1,3})$", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex LineShape();

    [GeneratedRegex(@"\+\+UE[45]\+Release-(?<line>\d{1,2}\.\d{1,3})", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex BranchName();
}

namespace FrameLedger.Domain.Detection;

/// <summary>
/// What an Unreal Engine title's shipping executable says about the engine it was built with (beta.10): read from the
/// file on disk, never from the game's process (CLAUDE.md rule 4 is about game memory).
/// </summary>
/// <remarks>
/// <para>
/// Two independent witnesses, because neither is enough on its own — measured 2026-09-27 on ten Unreal titles
/// (<c>05_DETECTION</c> §Engine version). The <b>numeric file version</b> (<c>VS_FIXEDFILEINFO</c>) is the engine's
/// <c>MAJOR.MINOR.PATCH</c> unless the studio replaced it (Cronos: 1.2.0.0, SILENT HILL 2: 1.0.0.5). The <b>branch name</b>
/// (<c>++UE5+Release-5.1</c>, a wide string the engine compiles in) names the engine's release line but not its patch, and
/// is missing wherever a studio renamed its branch or the executable is packed.
/// </para>
/// </remarks>
public sealed record UnrealBuildFacts
{
    /// <summary>The executable the facts were read from, relative to the install root — for the log line, not the decision.</summary>
    public string? ExecutableRelativePath { get; init; }

    /// <summary>The numeric file version's four parts, or null when the file carries none (all zero) or could not be read.</summary>
    public IReadOnlyList<int>? FixedFileVersion { get; init; }

    /// <summary>The version resource's <c>FileVersion</c> string, or null.</summary>
    public string? FileVersionText { get; init; }

    /// <summary>The version resource's <c>ProductVersion</c> string, or null.</summary>
    public string? ProductVersionText { get; init; }

    /// <summary>The version resource's <c>CompanyName</c>, or null.</summary>
    public string? CompanyName { get; init; }

    /// <summary>
    /// The engine release lines the executable names (<c>"5.1"</c> for <c>++UE5+Release-5.1</c>), distinct, from its bytes
    /// and from its version strings. Empty when none was found — which is not "not Unreal", only "no branch name".
    /// </summary>
    public IReadOnlyList<string> BranchVersions { get; init; } = [];
}

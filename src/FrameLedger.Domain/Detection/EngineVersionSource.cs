namespace FrameLedger.Domain.Detection;

/// <summary>
/// Where an engine version came from (beta.10, <c>games.engine_version_source</c>): the ids are stored, so they never change
/// meaning. The App says which witness a version rests on, because two of them can be wrong in different ways.
/// </summary>
public static class EngineVersionSource
{
    /// <summary>Unreal: the branch name and the numeric file version agree on MAJOR.MINOR — the patch is the file's.</summary>
    public const string UnrealBranchAndFile = "unreal_branch_file";

    /// <summary>Unreal: the branch name alone (the file version is the studio's own) — MAJOR.MINOR, no patch.</summary>
    public const string UnrealBranch = "unreal_branch";

    /// <summary>Unreal: the numeric file version alone, where nothing suggests the studio replaced it.</summary>
    public const string UnrealFileVersion = "unreal_file_version";

    /// <summary>The rule's own extractor, named as the rules file names it (<c>pe_file_version</c>, <c>strings_regex</c>, …).</summary>
    public static string Of(VersionExtractorType type) => type switch
    {
        VersionExtractorType.PeFileVersion => "pe_file_version",
        VersionExtractorType.PeProductVersionRegex => "pe_product_version_regex",
        VersionExtractorType.StringsRegex => "strings_regex",
        VersionExtractorType.ManifestField => "manifest_field",
        VersionExtractorType.UnrealBuild => "unreal_build",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "not a version extractor"),
    };
}

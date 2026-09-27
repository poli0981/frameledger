namespace FrameLedger.Domain.Detection;

/// <summary>How an engine's version is recovered once its rule matched.</summary>
public enum VersionExtractorType
{
    /// <summary>PE <c>FileVersion</c> of a named sibling file.</summary>
    PeFileVersion,

    /// <summary>First capture group of a regex over the executable's PE <c>ProductVersion</c>.</summary>
    PeProductVersionRegex,

    /// <summary>First capture group of a regex over the bounded strings scan.</summary>
    StringsRegex,

    /// <summary>A field in a store manifest. Not collected this phase.</summary>
    ManifestField,

    /// <summary>
    /// Unreal Engine's own build facts (beta.10): the shipping executable's numeric file version and the engine's branch
    /// name (<c>++UE5+Release-5.4</c>) inside it, weighed by <see cref="UnrealVersion.Decide"/>. Not the evaluator's to
    /// answer — it needs a second read of the executable, which <c>StaticGameDetector</c> makes only for the engine that
    /// matched — so <see cref="RuleEvaluator"/> returns null for it.
    /// </summary>
    UnrealBuild,
}

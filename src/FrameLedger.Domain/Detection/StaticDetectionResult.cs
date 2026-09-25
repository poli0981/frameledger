namespace FrameLedger.Domain.Detection;

/// <summary>
/// What static detection established about a game.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Every member here is a static hint.</strong> There is deliberately no
/// upscaler, frame-generation mode, RT/PT/RR flag or resolution:
/// <c>05_DETECTION</c> makes a static hint incapable of setting a runtime fact,
/// and a reflection test fails the build if such a member appears.
/// </para>
/// <para>
/// A null field means "not established", never "absent". The caller must not
/// overwrite a stored value with null, and must never overwrite a field whose
/// provenance is <see cref="DetectionProvenance.UserSupplied"/> — see
/// <see cref="ShouldWrite"/>.
/// </para>
/// </remarks>
public sealed record StaticDetectionResult
{
    /// <summary>Engine id, or null if undetermined or none matched.</summary>
    public string? EngineId { get; init; }

    /// <summary>Engine version, or null.</summary>
    public string? EngineVersion { get; init; }

    /// <summary>Platform id, or null.</summary>
    public string? PlatformId { get; init; }

    /// <summary>Capability ids the game SHIPS. Never a measurement.</summary>
    public required IReadOnlyList<string> CapabilityIds { get; init; }

    /// <summary>True when the engine walk stopped on a rule it could not decide.</summary>
    public bool EngineUndetermined { get; init; }

    /// <summary>True when the platform walk stopped on a rule it could not decide.</summary>
    public bool PlatformUndetermined { get; init; }

    /// <summary>The rules version this result was produced from — part of the cache key.</summary>
    public required string RulesVersion { get; init; }

    /// <summary>
    /// The executable references the Vulkan loader (<see cref="GameFileSnapshot.VulkanLoaderReferenced"/>): true,
    /// false, or null when the file could not be read for it. A static fact about the binary, never a claim
    /// about what it presented — the API a session ran on is <c>sessions.api</c>, measured (P4 PR-2).
    /// </summary>
    public bool? UsesVulkan { get; init; }

    /// <summary>The capability id the sweep stores for <see cref="UsesVulkan"/>; not a rule id, and not in the rules file.</summary>
    public const string VulkanCapabilityId = "vulkan";

    /// <summary>What the executable runs as (<see cref="ExecutableArchitecture"/>) — a fact about the file (beta.8).</summary>
    public string ExeArchitecture { get; init; } = ExecutableArchitecture.Unknown;

    /// <summary>The executable's PE <c>FileVersion</c>, or null. For an Unreal title it is often the ENGINE's version.</summary>
    public string? ExeFileVersion { get; init; }

    /// <summary>The executable's PE <c>ProductVersion</c>, or null.</summary>
    public string? ExeProductVersion { get; init; }

    /// <summary>The capability files the game ships, with their versions (<see cref="GameFileSnapshot.Libraries"/>).</summary>
    public IReadOnlyList<LibraryFile> Libraries { get; init; } = [];

    /// <summary>
    /// Whether a re-run may write <paramref name="detected"/> over a field whose
    /// current provenance is <paramref name="existing"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Detection never overwrites a user's value.</strong> No document
    /// stated this before, and without it the re-run that <c>05_DETECTION</c>
    /// §Caching triggers on every rules update would silently clobber every
    /// correction the user has ever made — a data-loss bug that only shows up on
    /// somebody else's machine, weeks later.
    /// </para>
    /// <para>
    /// An unrecognised provenance reads as user-supplied, so the failure
    /// direction is "we did not badge something we detected" rather than "we
    /// destroyed something they typed".
    /// </para>
    /// </remarks>
    public static bool ShouldWrite(DetectionProvenance existing, string? detected) =>
        detected is not null && existing == DetectionProvenance.Detected;
}

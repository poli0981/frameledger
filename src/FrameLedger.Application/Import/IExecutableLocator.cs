namespace FrameLedger.Application.Import;

/// <summary>
/// Which executable in an install directory is the game, for the stores that do not say (Steam, itch). A port so the
/// importer is tested without a file system; the adapter is <c>Infrastructure.Import.ExecutableLocator</c>. A guess,
/// shown on the review list for the user to accept or untick — never acted on silently.
/// </summary>
public interface IExecutableLocator
{
    /// <summary>The most plausible game executable under <paramref name="installDirectory"/>, or null when none is found.</summary>
    string? PickPrimary(string installDirectory);
}

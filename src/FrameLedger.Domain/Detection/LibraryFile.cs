namespace FrameLedger.Domain.Detection;

/// <summary>
/// One file a game ships that a capability rule names — <c>nvngx_dlss.dll</c>, <c>sl.interposer.dll</c>,
/// <c>ffx_fsr2_api_x64.dll</c>, <c>libxess.dll</c> — with the version its PE resource states (beta.8,
/// <c>games.library_versions</c>). What the game SHIPS, read from disk: a driver or the NVIDIA App can load another copy
/// at run time, which is <c>sessions.runtime_modules</c>' to say.
/// </summary>
/// <param name="CapabilityId">The capability rule that named the file (<c>dlss</c>, <c>fsr</c>, …).</param>
/// <param name="RelativePath">The file under the install root, forward-slashed.</param>
/// <param name="FileVersion">The numeric file version (<c>3.7.10.0</c>), or null when the file carries none or could not be read.</param>
/// <param name="ProductVersion">The product version string as the file states it, or null.</param>
public sealed record LibraryFile(string CapabilityId, string RelativePath, string? FileVersion, string? ProductVersion)
{
    /// <summary>The file's name without its folders.</summary>
    public string FileName => RelativePath[(RelativePath.LastIndexOf('/') + 1)..];
}

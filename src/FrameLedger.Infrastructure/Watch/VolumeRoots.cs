namespace FrameLedger.Infrastructure.Watch;

/// <summary>
/// The mounted volumes' roots for <c>ExecutableRelocator</c> (2026-09-22): <c>DriveInfo</c>, which is the BCL's own
/// enumeration and needs no new P/Invoke. Only ready fixed and removable drives — a network share or an optical drive
/// is not where a game library reappears under a new letter.
/// </summary>
public static class VolumeRoots
{
    public static IReadOnlyList<string> Ready()
    {
        List<string> roots = [];
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.IsReady && drive.DriveType is DriveType.Fixed or DriveType.Removable)
                {
                    roots.Add(drive.Name);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A drive that is going away between the enumeration and the question is not a root to look under.
            }
        }

        return roots;
    }
}

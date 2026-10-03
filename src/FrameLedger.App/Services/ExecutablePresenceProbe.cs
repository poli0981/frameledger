using System.IO;

namespace FrameLedger.App.Services;

/// <summary>Reads <see cref="ExecutablePresence"/> for a path.</summary>
public static class ExecutablePresenceProbe
{
    /// <summary>What is on disk for <paramref name="exePath"/>; a path that cannot be asked about reads as missing.</summary>
    public static ExecutablePresence Of(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return ExecutablePresence.Missing;
        }

        try
        {
            if (File.Exists(exePath))
            {
                return ExecutablePresence.Present;
            }

            string? root = Path.GetPathRoot(exePath);
            return root is { Length: > 0 } && !Directory.Exists(root) ? ExecutablePresence.DriveMissing : ExecutablePresence.Missing;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return ExecutablePresence.Missing;
        }
    }
}

using FrameLedger.Application.Import;

namespace FrameLedger.Infrastructure.Import;

/// <summary>
/// <see cref="IExecutableLocator"/> for the stores that do not name the game's executable (Steam, itch): walk the
/// install directory three levels deep, drop the helpers a game ships beside itself (crash handlers, installers,
/// redistributables, anti-cheat services, launchers' own updaters), and take the largest of what is left — a
/// shipping Unreal or Unity binary dwarfs its neighbours. A guess the review list shows; the user unticks what is
/// wrong. Never launched, never read past its size.
/// </summary>
public sealed class ExecutableLocator : IExecutableLocator
{
    public const int MaxDepth = 3;

    /// <summary>Name fragments of the helpers a game ships beside itself; a file whose name carries one is never the game.</summary>
    public static readonly IReadOnlyList<string> HelperFragments =
    [
        "crash", "handler", "unins", "setup", "install", "redist", "vc_redist", "dxsetup", "dotnet", "cef", "report", "updater",
        "launcher_helper", "easyanticheat", "beservice", "battleye", "steamerrorreporter", "ue4prereq", "uecc", "vcredist", "python", "node",
    ];

    public static bool IsHelper(string fileNameWithoutExtension)
    {
        ArgumentNullException.ThrowIfNull(fileNameWithoutExtension);
        foreach (string fragment in HelperFragments)
        {
            if (fileNameWithoutExtension.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public string? PickPrimary(string installDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);
        if (!Directory.Exists(installDirectory))
        {
            return null;
        }

        string? best = null;
        long bestSize = -1;
        foreach (string exe in Walk(installDirectory, 0))
        {
            if (IsHelper(Path.GetFileNameWithoutExtension(exe)))
            {
                continue;
            }

            long size;
            try
            {
                size = new FileInfo(exe).Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (size > bestSize)
            {
                best = exe;
                bestSize = size;
            }
        }

        return best;
    }

    private static IEnumerable<string> Walk(string dir, int depth)
    {
        IEnumerable<string> files;
        IEnumerable<string> dirs;
        try
        {
            files = Directory.EnumerateFiles(dir, "*.exe");
            dirs = depth < MaxDepth ? Directory.EnumerateDirectories(dir) : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (string f in files)
        {
            yield return f;
        }

        foreach (string d in dirs)
        {
            foreach (string f in Walk(d, depth + 1))
            {
                yield return f;
            }
        }
    }
}

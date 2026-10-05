// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;

namespace FrameLedger.App.Update;

/// <summary>
/// Whether a library FrameLedger loads into games is still loaded in one (beta.14). The Overlay is never unloaded from a live
/// game (<c>17_HOOK_ENGINE</c>), so after a capture is stopped mid-game, or an Agent is restarted, the game keeps
/// <c>current\FrameLedger.Overlay.dll</c> mapped until it exits — and Velopack, which swaps the whole install folder, then
/// fails half way, after the App has already quit. A mapped image cannot be opened for writing (Windows answers with a
/// sharing violation), so the check asks the file system and opens no process: nothing about the game is read.
/// </summary>
public static class PayloadInUse
{
    /// <summary>The libraries a game can hold: the Overlay FrameLedger injects, and the Vulkan layer a Vulkan game loads.</summary>
    public static IReadOnlyList<string> Files { get; } = ["FrameLedger.Overlay.dll", "FrameLedger.VkLayer.dll"];

    private const int _sharingViolation = unchecked((int)0x80070020);
    private const int _lockViolation = unchecked((int)0x80070021);
    private const int _userMappedFile = unchecked((int)0x800704C8);

    /// <summary>The first of <see cref="Files"/> under <paramref name="directory"/> that something still holds, or null.</summary>
    public static string? Find(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        foreach (string name in Files)
        {
            string path = Path.Combine(directory, name);
            if (File.Exists(path) && IsHeld(path))
            {
                return name;
            }
        }

        return null;
    }

    private static bool IsHeld(string path)
    {
        try
        {
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                return false;
            }
        }
        catch (IOException ex) when (ex.HResult is _sharingViolation or _lockViolation or _userMappedFile)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            // Not writable to this user: the updater could not replace it either.
            return true;
        }
    }
}

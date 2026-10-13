// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Play;
using Microsoft.Win32;

namespace FrameLedger.Infrastructure.Import;

/// <summary>
/// GOG GALAXY's program, as GOG's own registry entries name it (beta.21, Play): <c>HKLM\SOFTWARE\WOW6432Node\GOG.com\GalaxyClient</c>'s
/// <c>clientExecutable</c> (<c>GalaxyClient.exe</c>) in the folder its <c>paths\client</c> holds — read on the owner's PC on
/// 2026-10-13 as <c>C:\Program Files\GOG Galaxy</c>, the folder of the program its game shortcuts start. Read-only, whichever
/// hive and key the caller names (a test uses its own HKCU key); a key that is not there, or a file that is not, is "not
/// installed".
/// </summary>
public sealed class GogGalaxyLocator(RegistryHive hive = RegistryHive.LocalMachine, string keyPath = GogGalaxyLocator.DefaultKeyPath) : IGogGalaxyLocator
{
    public const string DefaultKeyPath = @"SOFTWARE\WOW6432Node\GOG.com\GalaxyClient";

    public string? ClientPath()
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using RegistryKey? client = baseKey.OpenSubKey(keyPath, writable: false);
            using RegistryKey? paths = client?.OpenSubKey("paths", writable: false);
            if (client?.GetValue("clientExecutable") is not string exe || paths?.GetValue("client") is not string folder
                || string.IsNullOrWhiteSpace(exe) || string.IsNullOrWhiteSpace(folder) || exe.IndexOfAny(['\\', '/']) >= 0)
            {
                return null;
            }

            string path = Path.Combine(folder, exe);
            return Path.IsPathFullyQualified(path) && File.Exists(path) ? path : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
        {
            return null;
        }
    }
}

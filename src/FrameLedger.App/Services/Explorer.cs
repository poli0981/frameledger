// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics;
using System.IO;

namespace FrameLedger.App.Services;

/// <summary>
/// Opens a folder, or a file selected in its folder, in Windows Explorer — the one way the App does it (beta.24 PR-2).
/// </summary>
/// <remarks>
/// <para>
/// <b>Started by its full path in the Windows folder, never by a name the <c>PATH</c> resolves</b> (SonarCloud
/// <c>csharpsquid:S4036</c>): five places started <c>"explorer.exe"</c> by name, so a program of that name earlier on
/// the search path would have been run in its place.
/// </para>
/// <para>
/// <b>The path is always quoted</b>, as two of the five already did: a bare one leaves Explorer to guess where it ends,
/// and its command line is a list of comma-separated switches. A trailing separator is dropped first, because
/// <c>\"</c> would read as an escaped quote.
/// </para>
/// </remarks>
internal static class Explorer
{
    /// <summary><c>%WINDIR%\explorer.exe</c>.</summary>
    public static string ExecutablePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

    /// <summary>Explorer's arguments that open <paramref name="folder"/>.</summary>
    public static string FolderArguments(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        return Quote(folder);
    }

    /// <summary>Explorer's arguments that open the folder <paramref name="file"/> is in, with it selected.</summary>
    public static string SelectArguments(string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        return "/select," + Quote(file);
    }

    /// <summary>Opens <paramref name="folder"/>; throws what <see cref="Process.Start(ProcessStartInfo)"/> throws.</summary>
    public static void OpenFolder(string folder) => Start(FolderArguments(folder));

    /// <summary>Opens the folder <paramref name="file"/> is in, with it selected; throws what <see cref="Process.Start(ProcessStartInfo)"/> throws.</summary>
    public static void ShowFile(string file) => Start(SelectArguments(file));

    private static void Start(string arguments)
    {
        using Process? _ = Process.Start(new ProcessStartInfo(ExecutablePath, arguments) { UseShellExecute = true });
    }

    private static string Quote(string path) => "\"" + Path.TrimEndingDirectorySeparator(path.Trim('"')) + "\"";
}

// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.ComponentModel;
using System.Diagnostics;
using FrameLedger.Application.Capture;

namespace FrameLedger.Infrastructure.Capture;

/// <summary>
/// One out-of-process look at which census-named modules the target has loaded, and what
/// version each file on disk carries.
/// </summary>
/// <remarks>
/// <para>
/// <b>Consumer-side, deliberately.</b> The writer could read the same thing on its watchdog, but
/// that costs <c>version.dll</c> as an import of the injected DLL, an allocation per module, and
/// a packed word per module in <c>FlWriterState.reserved[]</c> — twenty bytes for up to twenty
/// names does not fit. Out here it is <see cref="Process.Modules"/>, which is the loader's
/// list through documented PSAPI, plus <see cref="FileVersionInfo"/> on the path it names.
/// </para>
/// <para>
/// <b>And, since beta.24 (D64), where each file lives</b> — the game's folder, the graphics driver's own copy, or
/// Windows' (<see cref="RuntimeModuleSources"/>) — and every module under <c>%ProgramData%\NVIDIA\NGX</c>, whatever its
/// name: the NVIDIA App's DLSS override loads its libraries from there, and on the owner's Silent Hill 2 session (×6
/// frame generation through the override, 2026-10-13) none of them carried a census name.
/// </para>
/// <para>
/// <b>Never throws.</b> A target that is protected, elevated, or already gone yields an
/// unreadable snapshot with whatever was collected before the failure; the loop records that
/// rather than ending the session over a diagnostic.
/// </para>
/// </remarks>
public sealed class RuntimeModuleSnapshot : IRuntimeModuleSnapshot
{
    private readonly IReadOnlyCollection<string> _fileNames;

    /// <summary>A snapshot source over one fixed set of module names.</summary>
    /// <param name="fileNames">The module names to answer for — <see cref="CensusNames.ModuleFileNames"/> in the host.</param>
    public RuntimeModuleSnapshot(IReadOnlyCollection<string> fileNames) => _fileNames = fileNames ?? throw new ArgumentNullException(nameof(fileNames));

    /// <inheritdoc />
    RuntimeModuleSet IRuntimeModuleSnapshot.Take(int pid) => Take(pid, _fileNames);

    public static RuntimeModuleSet Take(int pid, IReadOnlyCollection<string> fileNames) =>
        Take(pid, fileNames, Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));

    /// <summary>The same with the two system folders handed in (tests).</summary>
    public static RuntimeModuleSet Take(int pid, IReadOnlyCollection<string> fileNames, string windowsDirectory, string programDataDirectory)
    {
        ArgumentNullException.ThrowIfNull(fileNames);
        var found = new List<RuntimeModuleInfo>();

        // A folder Windows did not name classifies nothing and catches nothing by path; the names are still answered.
        bool folders = !string.IsNullOrWhiteSpace(windowsDirectory) && !string.IsNullOrWhiteSpace(programDataDirectory);
        try
        {
            using Process target = Process.GetProcessById(pid);
            foreach (ProcessModule module in target.Modules)
            {
                using (module)
                {
                    if (module.ModuleName is not { } name || module.FileName is not { } path)
                    {
                        continue;
                    }

                    if (!fileNames.Contains(name, StringComparer.OrdinalIgnoreCase) && !(folders && RuntimeModuleSources.IsNgxOverrideFolder(path, programDataDirectory)))
                    {
                        continue;
                    }

                    found.Add(Describe(name, path) with
                    {
                        Source = folders ? RuntimeModuleSources.Classify(path, windowsDirectory, programDataDirectory) : RuntimeModuleSource.Unknown,
                    });
                }
            }

            return new RuntimeModuleSet(found, Snapshots: 1, Unreadable: 0);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException
                                       or NotSupportedException)
        {
            return new RuntimeModuleSet(found, Snapshots: 1, Unreadable: 1);
        }
    }

    /// <summary>
    /// The fixed-part version numbers, not the string: Streamline stamps its string as
    /// <c>2,8,0,0</c>, and a module with no version resource at all — Cyberpunk's
    /// <c>ffx_fsr3_x64.dll</c> — has neither, which is reported as null rather than as 0.0.0.0.
    /// </summary>
    private static RuntimeModuleInfo Describe(string name, string path)
    {
        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
            Version? parsed = info.FileVersion is null
                ? null
                : new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
            return new RuntimeModuleInfo(name, path, info.FileVersion, parsed);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new RuntimeModuleInfo(name, path, null, null);
        }
    }
}

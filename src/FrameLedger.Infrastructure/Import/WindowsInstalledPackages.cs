// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Infrastructure.Import;

/// <summary>
/// <see cref="IInstalledPackages"/> over <c>appmodel.h</c>'s documented calls — <c>PackageFamilyNameFromId</c> and
/// <c>GetPackagesByPackageFamily</c> — which answer for the user without elevation (measured on the owner's 11 games,
/// <c>spike-notes</c> §25). Hand-written <c>DllImport</c>s beside <c>ToolhelpProcessSnapshotSource</c>'s, for its reason:
/// the marshaling-off CsWin32 profile would hand back raw <c>PWSTR</c>s for the strings this class exists to read.
/// </summary>
public sealed class WindowsInstalledPackages : IInstalledPackages
{
    private const int _errorSuccess = 0;
    private const int _errorInsufficientBuffer = 122;
    private const int _familyNameCapacity = 128;    // PACKAGE_FAMILY_NAME_MAX_LENGTH is 64 + the terminator

    /// <summary>The one instance; it holds nothing.</summary>
    public static WindowsInstalledPackages Instance { get; } = new();

    /// <inheritdoc />
    public string? FamilyName(string name, string publisher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(publisher);
        var id = new PackageId { Name = name, Publisher = publisher };
        uint length = _familyNameCapacity;
        char[] buffer = new char[_familyNameCapacity];
        int result = PackageFamilyNameFromId(ref id, ref length, buffer);
        return result == _errorSuccess && length > 1 && length <= buffer.Length ? new string(buffer, 0, checked((int)length - 1)) : null;
    }

    /// <inheritdoc />
    public bool IsInstalled(string familyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyName);
        uint count = 0;
        uint bufferLength = 0;
        int result = GetPackagesByPackageFamily(familyName, ref count, IntPtr.Zero, ref bufferLength, IntPtr.Zero);

        // Asked with no buffers, an installed family answers "insufficient buffer" with its count; none answers success and 0.
        return result == _errorInsufficientBuffer || (result == _errorSuccess && count > 0);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PackageId
    {
        public uint Reserved;
        public uint ProcessorArchitecture;
        public ulong Version;
        public string Name;
        public string Publisher;
        public string? ResourceId;
        public string? PublisherId;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int PackageFamilyNameFromId(ref PackageId packageId, ref uint packageFamilyNameLength, [Out] char[] packageFamilyName);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetPackagesByPackageFamily(string packageFamilyName, ref uint count, IntPtr packageFullNames, ref uint bufferLength,
        IntPtr buffer);
}

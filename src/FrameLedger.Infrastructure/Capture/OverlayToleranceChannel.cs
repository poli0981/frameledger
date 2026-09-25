using System.Runtime.InteropServices;
using System.Security.Principal;
using FrameLedger.Application.Capture;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Shared;
using Microsoft.Win32.SafeHandles;

namespace FrameLedger.Infrastructure.Capture;

/// <summary>
/// D33 (owner decision 2026-09-26): <see cref="IOverlayToleranceChannel"/> as the named file mapping <c>fl_tolerance.h</c>
/// describes — <c>Local\FrameLedger.Tolerate.&lt;pid&gt;</c>, one <see cref="FlTolerance"/> naming the family — created by
/// the Agent before it asks the guard to inject, and held until the session ends.
/// </summary>
/// <remarks>
/// <para>
/// <b>The security descriptor is stated, not defaulted</b> (<see cref="Sddl"/>): owned by the user, READ for the user and
/// for nobody else. An elevated Agent's default DACL grants Administrators and SYSTEM and not the user
/// (<see cref="PipeAccessControl"/> records the same trap), and the Overlay in an unelevated game could then not open it —
/// which would read as the exception failing the moment its family's module loaded. Nobody may open it to write: the
/// Agent writes once, through the handle that created it.
/// </para>
/// <para>
/// <b>A name that already exists is not ours.</b> <c>CreateFileMapping</c> hands back a handle to an existing object with
/// <c>ERROR_ALREADY_EXISTS</c>; that is refused (null), so the session asks the guard to tolerate nothing and the blocked
/// game is refused as before — whatever that other mapping says, nothing is injected under it.
/// </para>
/// </remarks>
public sealed class OverlayToleranceChannel : IOverlayToleranceChannel
{
    private const uint _pageReadWrite = 0x04;
    private const uint _fileMapWrite = 0x0002;
    private const int _errorAlreadyExists = 183;

    private static readonly IntPtr _invalidHandle = new(-1);

    private readonly Action<string> _log;

    public OverlayToleranceChannel(Action<string>? log = null) => _log = log ?? (static _ => { });

    /// <summary>Owned by <paramref name="user"/>, a protected DACL granting <paramref name="user"/> read and nothing else.</summary>
    public static string Sddl(SecurityIdentifier user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return $"O:{user.Value}D:P(A;;GR;;;{user.Value})";
    }

    /// <summary>The mapping's bytes for <paramref name="family"/>, or null for a name the Overlay could never match (not printable ASCII, or too long).</summary>
    public static byte[]? Encode(string family)
    {
        if (string.IsNullOrWhiteSpace(family) || family.Length >= ToleranceLayout.NameLen || family.Any(static c => c is < ' ' or > '~'))
        {
            return null;
        }

        var tolerance = new FlTolerance { Magic = ToleranceLayout.Magic, Version = ToleranceLayout.Version, Count = 1 };
        unsafe
        {
            System.Text.Encoding.ASCII.GetBytes(family, new Span<byte>(tolerance.Families, ToleranceLayout.NameLen - 1));
        }

        byte[] bytes = new byte[Marshal.SizeOf<FlTolerance>()];
        MemoryMarshal.Write(bytes, in tolerance);
        return bytes;
    }

    /// <inheritdoc />
    public IDisposable? TryPublish(int pid, string family)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pid);
        byte[]? bytes = Encode(family);
        if (bytes is null)
        {
            _log($"tolerance: '{family}' cannot be named to the Overlay; no exception is in force for pid {pid}");
            return null;
        }

        string name = ToleranceLayout.MappingName(pid);
        byte[] descriptor = PipeAccessControl.DescriptorBytes(Sddl(PipeAccessControl.CurrentUser()));
        SafeMemoryMappedFileHandle mapping = Create(name, descriptor, (uint)bytes.Length, out int error);
        if (mapping.IsInvalid || error == _errorAlreadyExists)
        {
            mapping.Dispose();
            _log($"tolerance: {name} could not be created (error {error}); no exception is in force for pid {pid}");
            return null;
        }

        if (!Write(mapping, bytes))
        {
            mapping.Dispose();
            _log($"tolerance: {name} could not be written (error {Marshal.GetLastPInvokeError()}); no exception is in force for pid {pid}");
            return null;
        }

        _log($"tolerance: {name} names {family}");
        return new Published(mapping);
    }

    private static unsafe SafeMemoryMappedFileHandle Create(string name, byte[] descriptor, uint size, out int error)
    {
        fixed (byte* sd = descriptor)
        {
            var attributes = new SecurityAttributes
            {
                Length = sizeof(SecurityAttributes),
                SecurityDescriptor = (IntPtr)sd,
                InheritHandle = 0,
            };
            SafeMemoryMappedFileHandle mapping = CreateFileMappingW(_invalidHandle, ref attributes, _pageReadWrite, 0, size, name);
            error = Marshal.GetLastPInvokeError();
            return mapping;
        }
    }

    private static unsafe bool Write(SafeMemoryMappedFileHandle mapping, byte[] bytes)
    {
        IntPtr view = MapViewOfFile(mapping, _fileMapWrite, 0, 0, (nuint)bytes.Length);
        if (view == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            bytes.CopyTo(new Span<byte>((void*)view, bytes.Length));
            return true;
        }
        finally
        {
            _ = UnmapViewOfFile(view);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr SecurityDescriptor;
        public int InheritHandle;
    }

    /// <summary>The published mapping, held for the session; disposing it more than once is harmless.</summary>
    private sealed class Published(SafeMemoryMappedFileHandle mapping) : IDisposable
    {
        public void Dispose() => mapping.Dispose();
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeMemoryMappedFileHandle CreateFileMappingW(IntPtr file, ref SecurityAttributes attributes, uint protect,
        uint maximumSizeHigh, uint maximumSizeLow, string name);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr MapViewOfFile(SafeMemoryMappedFileHandle mapping, uint desiredAccess, uint offsetHigh, uint offsetLow,
        nuint bytesToMap);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnmapViewOfFile(IntPtr view);
}

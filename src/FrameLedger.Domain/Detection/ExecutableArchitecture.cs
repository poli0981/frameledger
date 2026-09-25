namespace FrameLedger.Domain.Detection;

/// <summary>
/// What an executable runs as (beta.8, <c>games.exe_machine</c>), from its PE headers: the COFF machine, and for a .NET
/// executable its CLR header's flags. The ids are stored, so they never change meaning.
/// </summary>
/// <remarks>
/// <para>
/// A .NET executable built "AnyCPU" carries an x86 COFF machine and runs as whatever process the OS starts — a 64-bit one
/// on 64-bit Windows — unless it prefers 32-bit (<c>COMIMAGE_FLAGS_32BITPREFERRED</c>) or requires it
/// (<c>COMIMAGE_FLAGS_32BITREQUIRED</c>, which is plain x86). Reading only the machine would call every FNA or MonoGame
/// title 32-bit.
/// </para>
/// <para>
/// FrameLedger's hook is an x64 DLL loaded with <c>LoadLibraryW</c> (CLAUDE.md rule 3): it can run in an x64 process and in
/// nothing else. <see cref="IsKnownNotHookable"/> names the ids for which that is known before the game ever runs; an id
/// this build could not establish is not one of them — the guard's own <c>TargetIsWow64</c> at a session's start stays
/// the check.
/// </para>
/// </remarks>
public static class ExecutableArchitecture
{
    /// <summary>AMD64: the one architecture the hook runs in.</summary>
    public const string X64 = "x64";

    /// <summary>I386, native (or .NET requiring 32-bit): a WOW64 process on 64-bit Windows.</summary>
    public const string X86 = "x86";

    /// <summary>ARM64: a native ARM process, which an x64 DLL cannot enter.</summary>
    public const string Arm64 = "arm64";

    /// <summary>ARMv7 (Thumb-2).</summary>
    public const string Arm = "arm";

    /// <summary>.NET, IL only: runs as a 64-bit process on 64-bit Windows.</summary>
    public const string AnyCpu = "anycpu";

    /// <summary>.NET, IL only, 32-bit preferred: runs as a 32-bit process.</summary>
    public const string AnyCpu32 = "anycpu32";

    /// <summary>A PE whose machine this build does not name.</summary>
    public const string Other = "other";

    /// <summary>Looked at, and not a PE this build could read: stored so the sweep does not look again every pass.</summary>
    public const string Unknown = "unknown";

    public const ushort MachineI386 = 0x014C;

    public const ushort MachineAmd64 = 0x8664;

    public const ushort MachineArm64 = 0xAA64;

    public const ushort MachineArmNt = 0x01C4;

    /// <summary><c>COMIMAGE_FLAGS_ILONLY</c>.</summary>
    public const uint ClrIlOnly = 0x0000_0001;

    /// <summary><c>COMIMAGE_FLAGS_32BITREQUIRED</c>.</summary>
    public const uint Clr32BitRequired = 0x0000_0002;

    /// <summary><c>COMIMAGE_FLAGS_32BITPREFERRED</c> (with 32BITREQUIRED clear it means "AnyCPU, prefer 32-bit").</summary>
    public const uint Clr32BitPreferred = 0x0002_0000;

    /// <summary>The id for a COFF machine and, when the PE carries a CLR header, its flags (null for a native PE).</summary>
    public static string Of(ushort machine, uint? clrFlags) => machine switch
    {
        MachineAmd64 => X64,
        MachineArm64 => Arm64,
        MachineArmNt => Arm,
        MachineI386 when clrFlags is { } f && (f & ClrIlOnly) != 0 && (f & Clr32BitRequired) == 0
            => (f & Clr32BitPreferred) != 0 ? AnyCpu32 : AnyCpu,
        MachineI386 => X86,
        _ => Other,
    };

    /// <summary>Known, before the game runs, to be a process the x64 hook cannot enter.</summary>
    public static bool IsKnownNotHookable(string? id) =>
        string.Equals(id, X86, StringComparison.Ordinal)
        || string.Equals(id, AnyCpu32, StringComparison.Ordinal)
        || string.Equals(id, Arm64, StringComparison.Ordinal)
        || string.Equals(id, Arm, StringComparison.Ordinal);
}

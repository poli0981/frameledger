using System.Runtime.InteropServices;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary><c>GetSystemTimes</c> in 100 ns units. Kernel INCLUDES idle, as the API reports it.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct SystemTimes(ulong Idle, ulong Kernel, ulong User);

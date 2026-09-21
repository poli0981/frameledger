using System.Runtime.InteropServices;

namespace FrameLedger.Application.Telemetry;

/// <summary>
/// One tick of the machine beside the GPU (2026-09-21): how busy the CPU was since the previous tick, how much
/// physical memory is in use, and — only where something can read it — the CPU's temperature. Every field is nullable
/// and a field nobody measured is null, never 0: <c>sessions.avg_cpu_load</c>, <c>avg_cpu_temp</c>,
/// <c>max_cpu_temp</c> and <c>avg_ram_mb</c> existed from schema 0001 with no producer, and a zero there would have
/// been a measurement nobody made.
/// </summary>
/// <param name="CpuLoadPct">
/// Busy time over elapsed time across every logical processor, 0–100, between this reading and the one before it. Null
/// on a source's first reading (there is no interval yet). This is TIME busy, the classic definition; Task Manager's
/// "utility" on a CPU with turbo can read higher, and the two are not the same number.
/// </param>
/// <param name="RamUsedMb">Physical memory in use machine-wide, MiB.</param>
/// <param name="CpuTempC">
/// The package temperature. Null unless the Agent runs elevated with PawnIO installed (<c>18_GPU_VENDOR_APIS</c> §L2):
/// no unprivileged API reads a CPU's thermal sensor.
/// </param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct SystemReading(double? CpuLoadPct, double? RamUsedMb, double? CpuTempC)
{
    public bool IsEmpty => CpuLoadPct is null && RamUsedMb is null && CpuTempC is null;
}

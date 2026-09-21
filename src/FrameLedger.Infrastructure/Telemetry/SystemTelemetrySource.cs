using FrameLedger.Application.Telemetry;
using Windows.Win32;
using Windows.Win32.System.SystemInformation;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// <see cref="ISystemTelemetrySource"/> over two unprivileged Win32 calls and one optional privileged reader
/// (2026-09-21): <c>GetSystemTimes</c> for how busy the CPU was between two ticks, <c>GlobalMemoryStatusEx</c> for the
/// physical memory in use, and an <see cref="ICpuTemperatureReader"/> where one could be opened.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not a PDH counter.</b> <c>\Processor Information(_Total)\% Processor Utility</c> is what Task Manager draws, and
/// it is a frequency-scaled figure that exceeds 100 under turbo; <c>% Processor Time</c> is this same busy-time ratio
/// with a localisation and a counter-registry dependency added. <c>GetSystemTimes</c> is the ratio with neither, and the
/// number is stated as what it is: time busy, all logical processors.
/// </para>
/// <para>
/// The first read has no interval behind it and reports no load — a null, never a zero. Kernel time INCLUDES idle time
/// in this API, so busy = (kernel + user) − idle.
/// </para>
/// <para>
/// The machine is sampled, never the game: nothing here opens the target process (CLAUDE.md rule 4 is about memory, and
/// this is further from it still — two system-wide counters).
/// </para>
/// </remarks>
public sealed class SystemTelemetrySource : ISystemTelemetrySource
{
    private const double _bytesPerMb = 1024.0 * 1024.0;

    private readonly ISystemCounters _counters;
    private readonly ICpuTemperatureReader? _temperature;
    private SystemTimes? _previous;
    private bool _disposed;

    public SystemTelemetrySource(ISystemCounters counters, ICpuTemperatureReader? temperature)
    {
        _counters = counters ?? throw new ArgumentNullException(nameof(counters));
        _temperature = temperature;
    }

    public bool CpuTemperatureAvailable => _temperature is not null;

    /// <summary>The shipped composition: the Win32 counters, and the LHM CPU reader only where it can work at all.</summary>
    public static SystemTelemetrySource Create() => new(new Win32SystemCounters(), LhmCpuTemperatureReader.TryOpen());

    public bool TryRead(out SystemReading reading)
    {
        reading = default;
        if (_disposed)
        {
            return false;
        }

        double? load = null;
        if (_counters.TryReadTimes(out SystemTimes now))
        {
            if (_previous is { } before)
            {
                load = Load(before, now);
            }

            _previous = now;
        }

        double? ram = _counters.TryReadMemoryInUseBytes(out ulong inUse) ? inUse / _bytesPerMb : null;
        double? temp = null;
        if (_temperature is not null)
        {
            try
            {
                temp = _temperature.Read();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A sensor library throwing on one tick is that tick without a temperature, never a lost sample.
                temp = null;
            }
        }

        reading = new SystemReading(load, ram, temp);
        return !reading.IsEmpty;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _temperature?.Dispose();
    }

    /// <summary>Busy time over elapsed time, 0–100; null when the counters did not advance (two reads inside one clock tick).</summary>
    public static double? Load(SystemTimes before, SystemTimes now)
    {
        ulong total = (now.Kernel - before.Kernel) + (now.User - before.User);
        ulong idle = now.Idle - before.Idle;
        if (total == 0 || idle > total)
        {
            return null;
        }

        return Math.Clamp(100.0 * (total - idle) / total, 0.0, 100.0);
    }

    private sealed class Win32SystemCounters : ISystemCounters
    {
        public bool TryReadTimes(out SystemTimes times)
        {
            times = default;
            if (!PInvoke.GetSystemTimes(out System.Runtime.InteropServices.ComTypes.FILETIME idle, out System.Runtime.InteropServices.ComTypes.FILETIME kernel, out System.Runtime.InteropServices.ComTypes.FILETIME user))
            {
                return false;
            }

            times = new SystemTimes(Ticks(idle), Ticks(kernel), Ticks(user));
            return true;
        }

        public bool TryReadMemoryInUseBytes(out ulong inUse)
        {
            inUse = 0;
            var status = new MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (!PInvoke.GlobalMemoryStatusEx(ref status) || status.ullTotalPhys < status.ullAvailPhys)
            {
                return false;
            }

            inUse = status.ullTotalPhys - status.ullAvailPhys;
            return true;
        }

        private static ulong Ticks(System.Runtime.InteropServices.ComTypes.FILETIME t) => ((ulong)(uint)t.dwHighDateTime << 32) | (uint)t.dwLowDateTime;
    }
}

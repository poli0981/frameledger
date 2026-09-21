namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>The seam under <see cref="SystemTelemetrySource"/>: the two Win32 reads, so the arithmetic is testable without a machine.</summary>
public interface ISystemCounters
{
    bool TryReadTimes(out SystemTimes times);

    bool TryReadMemoryInUseBytes(out ulong inUse);
}

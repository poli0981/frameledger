namespace FrameLedger.Application.Telemetry;

/// <summary>
/// The machine beside the GPU, read on the telemetry poller's tick. Separate from <see cref="IGpuTelemetrySource"/> on
/// purpose: the GPU layers compose per field with a capability bit and a layer attribution each
/// (<c>18_GPU_VENDOR_APIS</c>), and none of that machinery means anything for "how busy was the CPU".
/// </summary>
/// <remarks>
/// A read that fails returns false and the tick goes without: a sensor that stopped answering must never cost the
/// session its GPU sample, let alone the capture.
/// </remarks>
public interface ISystemTelemetrySource : IDisposable
{
    /// <summary>True when this source can ever produce a CPU temperature (elevated, PawnIO present, a sensor found).</summary>
    bool CpuTemperatureAvailable { get; }

    bool TryRead(out SystemReading reading);
}

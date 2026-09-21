namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>A CPU package temperature, where something privileged enough to read one exists; null when this tick had none.</summary>
public interface ICpuTemperatureReader : IDisposable
{
    double? Read();
}

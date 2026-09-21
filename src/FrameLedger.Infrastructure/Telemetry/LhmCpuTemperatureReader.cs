using LibreHardwareMonitor.Hardware;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// The CPU package temperature through LibreHardwareMonitor's CPU group — the one reading in this project that needs
/// privilege (<c>18_GPU_VENDOR_APIS</c> §L2: elevated, with the PawnIO driver installed). Opened only where it can work;
/// everywhere else <see cref="TryOpen"/> returns null and <c>cpu_temp</c> is N/A, which is the honest answer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Unmeasured on real hardware as of 2026-09-21.</b> The sensor choice below is LibreHardwareMonitor's naming
/// (<c>CPU Package</c> on Intel, <c>Core (Tctl/Tdie)</c> on AMD), exercised against a fake <see cref="ILhmComputer"/>.
/// No elevated run with PawnIO has been taken on the owner's machine; the first one is the measurement, and
/// <c>20_OPEN_QUESTIONS</c> carries it.
/// </para>
/// <para>
/// A separate <c>Computer</c> from L2's on purpose: L2 is GPU-only and unelevated by design
/// (<see cref="LhmComputerAdapter"/>), and enabling the CPU group there would make the unprivileged layer's behaviour
/// depend on privilege.
/// </para>
/// </remarks>
public sealed class LhmCpuTemperatureReader : ICpuTemperatureReader
{
    private readonly ILhmComputer _computer;
    private bool _disposed;

    public LhmCpuTemperatureReader(ILhmComputer computer)
    {
        _computer = computer ?? throw new ArgumentNullException(nameof(computer));
        _computer.Open();
    }

    /// <summary>Null unless the process is elevated AND PawnIO is installed AND the CPU group opened.</summary>
    public static LhmCpuTemperatureReader? TryOpen()
    {
        if (!LhmEnvironment.IsElevated || LhmEnvironment.IsPawnIoInstalled != true)
        {
            return null;
        }

        try
        {
            return new LhmCpuTemperatureReader(new LhmComputerAdapter(enableCpuAndMemory: true, enableGpu: false));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    public double? Read()
    {
        if (_disposed)
        {
            return null;
        }

        _computer.Update();
        return Pick(_computer.Hardware);
    }

    /// <summary>
    /// The package sensor where the CPU names one, else the hottest core: a session's <c>max_cpu_temp</c> is about the
    /// hottest the part got, and a package sensor is that by definition.
    /// </summary>
    public static double? Pick(IReadOnlyList<IHardware> hardware)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        double? package = null;
        double? hottestCore = null;
        foreach (IHardware cpu in hardware.Where(static h => h.HardwareType == HardwareType.Cpu))
        {
            foreach (ISensor sensor in SensorMap.AllSensors(cpu))
            {
                if (sensor.SensorType != SensorType.Temperature || sensor.Value is not float value || value <= 0)
                {
                    continue;
                }

                string name = sensor.Name ?? string.Empty;
                if (name.Contains("Package", StringComparison.OrdinalIgnoreCase) || name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) || name.Contains("Tdie", StringComparison.OrdinalIgnoreCase))
                {
                    package = package is { } p ? Math.Max(p, value) : value;
                }
                else if (name.Contains("Core", StringComparison.OrdinalIgnoreCase) && !name.Contains("Distance", StringComparison.OrdinalIgnoreCase))
                {
                    hottestCore = hottestCore is { } c ? Math.Max(c, value) : value;
                }
            }
        }

        return package ?? hottestCore;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _computer.Close();
    }
}

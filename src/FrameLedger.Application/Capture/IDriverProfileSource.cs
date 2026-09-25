namespace FrameLedger.Application.Capture;

/// <summary>
/// The NVIDIA driver profile an executable runs under (beta.8, 2026-09-25): which profile the driver applies to it, and
/// the values that profile gives the DLSS and frame-generation override settings the NVIDIA App writes
/// (<see cref="NvidiaDriverSettings"/>).
/// </summary>
/// <remarks>
/// Read from the driver's own settings store, by the executable's path, through the NVAPI bridge's read-only DRS entry
/// point — never from the game's process or memory (CLAUDE.md rule 4), and nothing is ever written back. A machine
/// without an NVIDIA driver answers <see cref="DriverProfileOutcome.Degraded"/>; the call never throws.
/// </remarks>
public interface IDriverProfileSource
{
    DriverProfileReading Read(string exePath);
}

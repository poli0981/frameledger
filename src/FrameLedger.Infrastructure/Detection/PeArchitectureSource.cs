using FrameLedger.Application.Detection;

namespace FrameLedger.Infrastructure.Detection;

/// <summary><see cref="IExecutableArchitectureSource"/> over <see cref="PeImports.ReadArchitecture(string)"/>: bounded, read-only, never throws for a bad file.</summary>
public sealed class PeArchitectureSource : IExecutableArchitectureSource
{
    public string Read(string exePath) => PeImports.ReadArchitecture(exePath);
}

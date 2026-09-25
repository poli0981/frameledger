using FrameLedger.Domain.Detection;

namespace FrameLedger.Application.Detection;

/// <summary>
/// What an executable runs as, read from its PE headers (beta.8): an <see cref="ExecutableArchitecture"/> id. Read-only
/// file I/O on the executable — never its process (CLAUDE.md rule 4).
/// </summary>
public interface IExecutableArchitectureSource
{
    /// <summary>The id; <see cref="ExecutableArchitecture.Unknown"/> when the file cannot be read as a PE.</summary>
    string Read(string exePath);
}

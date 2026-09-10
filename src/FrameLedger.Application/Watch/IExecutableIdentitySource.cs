using FrameLedger.Domain.Consent;

namespace FrameLedger.Application.Watch;

/// <summary>
/// The executable as it is on disk right now, for the orchestrator's <c>RecordRequest.Observed</c>. The adapter is
/// <c>Infrastructure.Io.ExecutableIdentity</c>; a port because the orchestrator is tested without a file system.
/// </summary>
public interface IExecutableIdentitySource
{
    /// <summary>The fingerprint of <paramref name="normalisedExePath"/>, or null when the file cannot be read.</summary>
    ExecutableFingerprint? Read(string normalisedExePath);
}

using FrameLedger.Application.Watch;
using FrameLedger.Domain.Consent;
using FrameLedger.Infrastructure.Io;

namespace FrameLedger.Infrastructure.Watch;

/// <summary>The orchestrator's view of the file on disk: <see cref="ExecutableIdentity.Read"/> behind the port.</summary>
public sealed class ExecutableIdentitySource : IExecutableIdentitySource
{
    /// <inheritdoc />
    public ExecutableFingerprint? Read(string normalisedExePath) => ExecutableIdentity.Read(normalisedExePath);
}

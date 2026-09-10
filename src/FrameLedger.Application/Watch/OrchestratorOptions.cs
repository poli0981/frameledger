namespace FrameLedger.Application.Watch;

/// <summary>What the orchestrator needs to know that no port tells it.</summary>
public sealed record OrchestratorOptions
{
    /// <summary>The Overlay beside the guard (§S22); the Agent's composition root supplies it.</summary>
    public required string PayloadPath { get; init; }

    /// <summary>The watcher's cadence; <c>01_ARCHITECTURE</c> §Lifecycle says 1 Hz and ~0 % CPU.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);
}

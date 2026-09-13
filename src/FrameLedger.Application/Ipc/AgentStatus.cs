using FrameLedger.Shared.Ipc;

namespace FrameLedger.Application.Ipc;

/// <summary>
/// The Agent's state as <c>GetStatus</c> reports it: the sessions running right now. Immutable, swapped whole,
/// so the pipe's thread reads a consistent snapshot without a lock (<c>04_CAPTURE</c> §Threading model).
/// </summary>
public sealed record AgentStatus(IReadOnlyList<ActiveSession> Sessions)
{
    public const string IdleState = "idle";

    /// <summary>A session runs and nothing is hooked — a Tier-2 row in the making, or a loop still before its attach.</summary>
    public const string RecordingState = "recording";

    /// <summary>At least one ring is attached.</summary>
    public const string CapturingState = "capturing";

    public static AgentStatus Idle { get; } = new([]);

    public string State =>
        Sessions.Any(static s => s.Tier == 1) ? CapturingState
        : Sessions.Count > 0 ? RecordingState
        : IdleState;

    public StatusAck ToAck()
    {
        ActiveSession? primary = Sessions.FirstOrDefault(static s => s.Tier == 1) ?? (Sessions.Count > 0 ? Sessions[0] : null);
        return new StatusAck(State, primary, primary?.Tier, Sessions);
    }
}

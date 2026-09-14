using FrameLedger.App.Services;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>An <see cref="IAgentLink"/> a test drives: a scripted answer per request, and events raised on demand.</summary>
internal sealed class FakeAgentLink : IAgentLink
{
    public AgentConnectionState State { get; set; } = AgentConnectionState.Connected;

    public StatusAck? Status { get; set; }

    public HelloAck? Hello { get; set; } = new("agent", IpcProtocol.Version, 1, false, "b", false, "l1", false, Shared.Safety.SafetyDisclosure.Version);

    public bool IsConnected { get; set; } = true;

    public List<(string Type, object Payload)> Sent { get; } = [];

    /// <summary>Every <see cref="SetLaunchHold"/> call, in order (P4 PR-5: the apply holds, a failed apply releases).</summary>
    public List<bool> Holds { get; } = [];

    public void SetLaunchHold(bool hold) => Holds.Add(hold);

    public Func<string, object, IpcEnvelope> Answer { get; set; } = static (_, _) => throw new InvalidOperationException("no answer scripted");

    public event EventHandler? Changed;

    public event EventHandler<AgentEventArgs>? EventReceived;

    public Task<IpcEnvelope> RequestAsync<TRequest>(string type, TRequest payload, CancellationToken ct = default)
        where TRequest : class
    {
        Sent.Add((type, payload));
        return Task.FromResult(Answer(type, payload));
    }

    public void Raise<T>(string type, T payload)
        where T : class => EventReceived?.Invoke(this, new AgentEventArgs(IpcCodec.Decode(IpcCodec.Encode(type, null, payload))));

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public static IpcEnvelope Envelope<T>(string type, T payload)
        where T : class => IpcCodec.Decode(IpcCodec.Encode(type, "1", payload));
}

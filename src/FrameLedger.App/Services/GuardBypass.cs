using System.IO;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Shared.Ipc;
using FrameLedger.Shared.Safety;
using Serilog;

namespace FrameLedger.App.Services;

/// <summary>
/// The per-game guard bypass as the App performs it (owner decision 2026-09-21, <c>19_SAFETY</c> §The user's bypass):
/// show the disclosure, and only when it was accepted by both of its acts ask the Agent to record it. The App writes
/// nothing itself — the acknowledgement is stamped by the Agent, over the pipe, exactly as consent is — and the Agent
/// re-checks the disclosure version and the executable, because the pipe is not a trust boundary.
/// </summary>
/// <remarks>
/// There is no "all games" form of this and no setting that pre-accepts it: every game is its own dialog.
/// </remarks>
public sealed class GuardBypass
{
    private readonly IAgentRequests _agent;
    private readonly IGuardBypassPrompt _prompt;

    public GuardBypass(IAgentRequests agent, IGuardBypassPrompt prompt)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
    }

    public async Task<GuardBypassResult> EnableAsync(long gameId, string gameName, string? finding, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);
        if (!_agent.IsConnected)
        {
            return new GuardBypassResult(GuardBypassOutcome.AgentUnavailable);
        }

        if (!await _prompt.ShowAsync(gameName, finding, ct).ConfigureAwait(true))
        {
            return new GuardBypassResult(GuardBypassOutcome.Declined);
        }

        return await SendAsync(new SetGuardBypassRequest(gameId, Enabled: true, GuardBypassDisclosure.Version), GuardBypassOutcome.Enabled, ct).ConfigureAwait(true);
    }

    public Task<GuardBypassResult> DisableAsync(long gameId, CancellationToken ct = default) =>
        _agent.IsConnected
            ? SendAsync(new SetGuardBypassRequest(gameId, Enabled: false, null), GuardBypassOutcome.Disabled, ct)
            : Task.FromResult(new GuardBypassResult(GuardBypassOutcome.AgentUnavailable));

    private async Task<GuardBypassResult> SendAsync(SetGuardBypassRequest request, GuardBypassOutcome onWritten, CancellationToken ct)
    {
        try
        {
            IpcEnvelope ack = await _agent.RequestAsync(IpcMessageType.SetGuardBypass, request, ct).ConfigureAwait(true);
            if (!string.Equals(ack.Type, IpcMessageType.GuardBypassAck, StringComparison.Ordinal))
            {
                return new GuardBypassResult(GuardBypassOutcome.Failed, "UnexpectedAck:" + ack.Type);
            }

            GuardBypassAck? outcome = IpcCodec.Payload<GuardBypassAck>(ack);
            return outcome is not null && outcome.Enabled == request.Enabled
                ? new GuardBypassResult(onWritten, outcome.Outcome)
                : new GuardBypassResult(GuardBypassOutcome.Failed, outcome?.Outcome ?? "EmptyAck");
        }
        catch (Exception ex) when (ex is IpcRequestException or TimeoutException or IOException or InvalidOperationException)
        {
            Log.Warning(ex, "bypass: SetGuardBypass(game {GameId}, {Enabled}) did not complete", request.GameId, request.Enabled);
            return new GuardBypassResult(GuardBypassOutcome.Failed, ex is IpcRequestException ipc ? ipc.Code ?? IpcMessageType.Error : ex.GetType().Name);
        }
    }
}

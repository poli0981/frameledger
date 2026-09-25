using System.IO;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Shared.Ipc;
using FrameLedger.Shared.Safety;
using Serilog;

namespace FrameLedger.App.Services;

/// <summary>
/// D33 (owner decision 2026-09-26) from the App's side: the version handshake (the dialog is not opened when the Agent
/// grants against other text), the disclosure, and the one message that may ask for a user-mode exception
/// (<c>SetAntiCheatException</c>). Like <see cref="HookingConsent"/> it asserts nothing: it sends the version it showed,
/// and the Agent re-checks the option, the block, a tolerant pre-scan of the file on disk and the session count, and
/// grants or refuses (<c>07_IPC</c> §The pipe is not a trust boundary).
/// </summary>
public sealed class AntiCheatExceptions
{
    private readonly IAgentRequests _agent;
    private readonly IAntiCheatExceptionPrompt _prompt;

    public AntiCheatExceptions(IAgentRequests agent, IAntiCheatExceptionPrompt prompt)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
    }

    /// <summary>Show the disclosure for the game in <paramref name="facts"/> and, on its acceptance, ask the Agent for the exception.</summary>
    public async Task<AntiCheatExceptionResult> GrantAsync(long gameId, AntiCheatExceptionFacts facts, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(facts);
        HelloAck? hello = _agent.Hello;
        if (hello is null || !_agent.IsConnected)
        {
            return AntiCheatExceptionResult.Of(AntiCheatExceptionOutcome.AgentUnavailable);
        }

        if (!string.Equals(hello.ExceptionDisclosureVersion, AntiCheatExceptionDisclosure.Version, StringComparison.Ordinal))
        {
            // Showing text the Agent would not grant against is a dialog whose button cannot mean what it says.
            Log.Warning("exception: the Agent grants against {Theirs}, this app shows {Ours}; the dialog was not opened",
                hello.ExceptionDisclosureVersion, AntiCheatExceptionDisclosure.Version);
            return new AntiCheatExceptionResult(AntiCheatExceptionOutcome.VersionMismatch, hello.ExceptionDisclosureVersion);
        }

        if (!await _prompt.ShowAsync(facts, ct).ConfigureAwait(true))
        {
            return AntiCheatExceptionResult.Of(AntiCheatExceptionOutcome.Declined);
        }

        return await SendAsync(new SetAntiCheatExceptionRequest(gameId, Enabled: true, AntiCheatExceptionDisclosure.Version), ct).ConfigureAwait(true);
    }

    /// <summary>Withdraw the game's exception; no dialog — a withdrawal only takes hooking away.</summary>
    public Task<AntiCheatExceptionResult> WithdrawAsync(long gameId, CancellationToken ct = default) =>
        _agent.IsConnected
            ? SendAsync(new SetAntiCheatExceptionRequest(gameId, Enabled: false, null), ct)
            : Task.FromResult(AntiCheatExceptionResult.Of(AntiCheatExceptionOutcome.AgentUnavailable));

    private async Task<AntiCheatExceptionResult> SendAsync(SetAntiCheatExceptionRequest request, CancellationToken ct)
    {
        try
        {
            IpcEnvelope ack = await _agent.RequestAsync(IpcMessageType.SetAntiCheatException, request, ct).ConfigureAwait(true);
            return Interpret(ack, request.Enabled);
        }
        catch (Exception ex) when (ex is IpcRequestException or TimeoutException or IOException or InvalidOperationException)
        {
            Log.Warning(ex, "exception: SetAntiCheatException(game {GameId}, {Enabled}) did not complete", request.GameId, request.Enabled);
            string? code = (ex as IpcRequestException)?.Code;
            AntiCheatExceptionOutcome outcome = code switch
            {
                IpcErrorCode.DisclosureVersionMismatch => AntiCheatExceptionOutcome.VersionMismatch,
                IpcErrorCode.ExceptionsOff => AntiCheatExceptionOutcome.OptionOff,
                _ => AntiCheatExceptionOutcome.Failed,
            };
            return new AntiCheatExceptionResult(outcome, code ?? ex.GetType().Name);
        }
    }

    private static AntiCheatExceptionResult Interpret(IpcEnvelope ack, bool granting)
    {
        if (string.Equals(ack.Type, IpcMessageType.Refused, StringComparison.Ordinal))
        {
            RefusedAck? refused = IpcCodec.Payload<RefusedAck>(ack);
            return new AntiCheatExceptionResult(AntiCheatExceptionOutcome.Refused, refused?.Reason, refused);
        }

        if (!string.Equals(ack.Type, IpcMessageType.AntiCheatExceptionAck, StringComparison.Ordinal))
        {
            return new AntiCheatExceptionResult(AntiCheatExceptionOutcome.Failed, "UnexpectedAck:" + ack.Type);
        }

        AntiCheatExceptionAck? answer = IpcCodec.Payload<AntiCheatExceptionAck>(ack);
        if (answer is null)
        {
            return new AntiCheatExceptionResult(AntiCheatExceptionOutcome.Failed, "EmptyAck");
        }

        // A withdrawal of an exception that was not in force is still the state the user asked for.
        return granting
            ? answer.Granted ? new(AntiCheatExceptionOutcome.Granted, answer.Outcome) : new(AntiCheatExceptionOutcome.Failed, answer.Outcome)
            : answer.Granted ? new(AntiCheatExceptionOutcome.Failed, answer.Outcome) : new(AntiCheatExceptionOutcome.Withdrawn, answer.Outcome);
    }
}

using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Shared.Ipc;
using Serilog;

namespace FrameLedger.App.Services;

/// <summary>
/// Delete all sessions (beta.11, owner decision D40), of one game from its page or of every game from Settings ▸ Data. The
/// sessions are the Agent's rows (<c>06_DATA_MODEL</c> §Writer ownership), so the App asks it over the pipe, as Tools ▸
/// Database maintenance asks for the retention sweep; the Agent refuses while a session it would delete under is being
/// recorded. The confirmation is the caller's to ask first.
/// </summary>
public sealed class SessionDeletion
{
    private readonly IAgentRequests _agent;

    public SessionDeletion(IAgentRequests agent) => _agent = agent ?? throw new ArgumentNullException(nameof(agent));

    /// <summary>Every session of <paramref name="gameId"/>, or of every game when it is null.</summary>
    public async Task<SessionDeletionResult> DeleteAsync(long? gameId, CancellationToken ct = default)
    {
        if (!_agent.IsConnected)
        {
            return new(SessionDeletionOutcome.AgentUnavailable);
        }

        try
        {
            IpcEnvelope envelope = await _agent.RequestAsync(IpcMessageType.DeleteSessions, new DeleteSessionsRequest(gameId), ct).ConfigureAwait(true);
            DeleteSessionsAck ack = IpcCodec.Payload<DeleteSessionsAck>(envelope) ?? throw new InvalidOperationException("DeleteSessions answered without a payload");
            Log.Information("sessions: deleted {Sessions} session(s) of {Games} game(s) ({Scope})", ack.Sessions, ack.Games, gameId is null ? "every game" : "game " + gameId);
            return new(SessionDeletionOutcome.Deleted, ack.Games, ack.Sessions);
        }
        catch (IpcRequestException ex) when (string.Equals(ex.Code, IpcErrorCode.SessionRunning, StringComparison.Ordinal))
        {
            return new(SessionDeletionOutcome.SessionRunning);
        }
        catch (IpcRequestException ex) when (string.Equals(ex.Code, IpcErrorCode.UnknownType, StringComparison.Ordinal))
        {
            Log.Warning("sessions: the Agent does not know DeleteSessions ({Message})", ex.Message);
            return new(SessionDeletionOutcome.AgentTooOld);
        }
        catch (Exception ex) when (ex is IpcRequestException or InvalidOperationException or TimeoutException)
        {
            Log.Warning("sessions: DeleteSessions failed ({Message})", ex.Message);
            return new(SessionDeletionOutcome.Failed, Detail: ex.Message);
        }
    }
}

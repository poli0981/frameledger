namespace FrameLedger.Application.Ipc;

/// <summary>
/// <c>Shutdown</c>'s one effect (<c>07_IPC</c> §Messages): ask the host to stop gracefully — sessions finalize
/// inside the host's grace window, as they do on Ctrl+C. The Agent adapts its <c>IHostApplicationLifetime</c>;
/// under <c>--console</c> there is no host and the request is a logged no-op.
/// </summary>
public interface IAgentLifetime
{
    void RequestShutdown();
}

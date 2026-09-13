using FrameLedger.Application.Ipc;
using Serilog;

namespace FrameLedger.Agent.Hosting;

/// <summary>Under <c>--console</c> there is no host to stop; the request is logged and nothing else happens.</summary>
internal sealed class NoAgentLifetime : IAgentLifetime
{
    public void RequestShutdown() => Log.Information("pipe: Shutdown requested, but this process hosts no service (--console); ignored");
}

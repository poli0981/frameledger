using FrameLedger.Application.Ipc;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace FrameLedger.Agent.Hosting;

/// <summary><c>Shutdown</c> under <c>--serve</c>: the host stops, sessions finalize inside its grace window.</summary>
internal sealed class HostAgentLifetime(IHostApplicationLifetime host) : IAgentLifetime
{
    public void RequestShutdown()
    {
        Log.Information("pipe: Shutdown requested by a client; stopping the host");
        host.StopApplication();
    }
}

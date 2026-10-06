// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.App.Update;
using FrameLedger.Infrastructure.Ipc;
using Microsoft.Extensions.DependencyInjection;

namespace FrameLedger.App.Services;

/// <summary>
/// Every part of the App's composition that reaches past its ledger — the Agent over the pipe and the launcher behind it,
/// the Agent's maintenance flags, the Run entry, the updater — registered in one place, so that a viewer's composition
/// (beta.15, D52) can be seen, and is tested (<c>MachineFacingServicesTests</c>), to hold none of it.
/// </summary>
internal static class MachineFacingServices
{
    public static void Add(IServiceCollection services, UiMode mode)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(mode);
        services.AddSingleton(mode);
        if (mode.IsViewer)
        {
            AddViewer(services);
        }
        else
        {
            AddProfile(services);
        }

        // What the Agent runs right now (2026-09-23), for the pages rebuilt on every visit; built before the connection's
        // first round by AgentConnectionHostedService, so it never misses a session. A viewer's is always empty.
        services.AddSingleton(static sp => new LiveSessions(sp.GetRequiredService<IAgentLink>()));
    }

    /// <summary>
    /// The Agent over the pipe (07_IPC §Client behavior): connect, start it when it is not there, tell the shell; the Run
    /// entry; the layer registration and the logon task through the Agent's flags (P3 PR-8b); the updater (P4 PR-5).
    /// </summary>
    private static void AddProfile(IServiceCollection services)
    {
        // The admin mode's prompt (beta.10) belongs to the main window; the handle is read when a start asks, never earlier.
        services.AddSingleton<IAgentLauncher>(static sp => AgentLauncher.ForApp(() => sp.GetRequiredService<ShellHost>().Handle));
        services.AddSingleton(static sp => new AgentConnection(
            sp.GetRequiredService<IAgentLauncher>(),
            static () => new PipeClient(),
            new AgentConnectionOptions(),
            UiIdentity.Version));
        services.AddHostedService<AgentConnectionHostedService>();
        services.AddSingleton<IAgentRequests>(static sp => sp.GetRequiredService<AgentConnection>());
        services.AddSingleton<IAgentLink>(static sp => sp.GetRequiredService<AgentConnection>());

        services.AddSingleton<IRunAtLogon, RunAtLogonRegistry>();
        services.AddSingleton<IAgentTool, AgentTool>();

        // The startup check is a hosted service; after an update's restart it also repairs the logon task.
        services.AddSingleton<IUpdateClient, VelopackUpdateClient>();
        services.AddHostedService<UpdateHostedService>();
    }

    /// <summary>A viewer: a link that is never connected and nothing that could connect it, and nothing that changes this PC.</summary>
    private static void AddViewer(IServiceCollection services)
    {
        services.AddSingleton<ViewerAgentLink>();
        services.AddSingleton<IAgentRequests>(static sp => sp.GetRequiredService<ViewerAgentLink>());
        services.AddSingleton<IAgentLink>(static sp => sp.GetRequiredService<ViewerAgentLink>());
        services.AddSingleton<IRunAtLogon, ViewerRunAtLogon>();
        services.AddSingleton<IAgentTool, ViewerAgentTool>();
        services.AddSingleton<IUpdateClient, ViewerUpdateClient>();
    }
}

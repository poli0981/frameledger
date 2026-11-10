// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FrameLedger.Agent.Tests;

/// <summary>
/// .NET 11, the main line's runtime since 0.1.0-beta.37 (D94): a host whose <see cref="BackgroundService"/> throws stops
/// AND fails - <c>RunAsync</c> rethrows the service's exception, where .NET 10 completed it and the Agent exited 0. The
/// Agent's <c>--serve</c> awaits <c>RunAsync</c> and catches nothing, so that exception is what takes <c>Program.Main</c>
/// to its crash report (<c>10_LOGGING</c> §Crash handling). Microsoft documents the change, three alphas shipped with it,
/// and nothing had observed it until this test (<c>spike-notes</c> §24).
/// </summary>
public sealed class HostBackgroundFailureTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AHostWhoseBackgroundServiceThrowsFailsRunAsyncWithThatException()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddHostedService(static _ => new Failing());
        using IHost host = builder.Build();

        // Bounded: a runtime that stopped the host without failing it would complete here, and one that did neither
        // would otherwise hang the suite.
        string? failure = null;
        try
        {
            await host.RunAsync(Ct).WaitAsync(TimeSpan.FromSeconds(30), Ct);
        }
        catch (InvalidOperationException ex)
        {
            failure = ex.Message;
        }

        failure.Should().Be("the watcher broke", "the host stops and fails when a background service throws");
    }

    private sealed class Failing : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            throw new InvalidOperationException("the watcher broke");
        }
    }
}

// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Reflection;
using FluentAssertions;
using FrameLedger.App.Update;
using Velopack;

namespace FrameLedger.App.Tests.Update;

/// <summary>
/// beta.14 (the update audit): what <see cref="VelopackHooks.Configure"/> hands Velopack. Read through Velopack 1.2.0's private
/// fields, because the builder exposes no getters — pinned to that version (<c>Directory.Packages.props</c>): an upgrade that
/// renames them turns this red rather than letting the apply-at-start come back unseen.
/// </summary>
public sealed class VelopackHooksTests
{
    private static object? Field(VelopackApp app, string name) =>
        typeof(VelopackApp).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(app)
        ?? throw new InvalidOperationException($"VelopackApp.{name} is gone: re-read the pinned version's builder");

    [Fact]
    public void ADownloadedUpdateIsNeverAppliedAtStartBehindTheAppsBack()
    {
        VelopackApp app = VelopackHooks.Configure(VelopackApp.Build());

        Field(app, "_autoApply").Should().Be(false, "Velopack applied a downloaded package at the next start, before FR-12 or the Agent's stop could run");
    }

    [Fact]
    public void VelopacksDiagnosticsGoToTheAppsLogAndTheAgentIsAskedToStopBeforeAnUpdate()
    {
        VelopackApp app = VelopackHooks.Configure(VelopackApp.Build());

        Field(app, "_customLogger").Should().BeOfType<VelopackSerilogLogger>();
        Field(app, "_obsolete").Should().NotBeNull("OnBeforeUpdateFastCallback stops the Agent before Velopack replaces the version");
        Field(app, "_uninstall").Should().NotBeNull();
        Field(app, "_restarted").Should().NotBeNull();
    }
}

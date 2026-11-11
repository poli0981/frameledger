// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Telemetry;
using FrameLedger.Infrastructure.Telemetry;

namespace FrameLedger.Infrastructure.Tests.Telemetry;

/// <summary>
/// The real library, on whatever this machine is. Asserts the CONTRACT, never the
/// hardware: CI has no GPU and a dev box may have any vendor's.
/// </summary>
/// <remarks>
/// <c>Category=Integration</c> because it opens vendor driver paths; <c>build.ps1
/// -SkipIntegration</c> excludes it on CI. The §M5 measurement itself is
/// <c>FrameLedger.CaptureHost probe-lhm</c> and its result is recorded in
/// <c>spike-notes</c> §10 — this case only proves the port behaves under the real library.
/// </remarks>
[Trait("Category", "Integration")]
[Collection(LibreHardwareMonitorCollection.Name)]
public sealed class LhmRealHardwareTests
{
    [Fact]
    public void TwoUsersOfTheRealLibraryOneLetsGoAndTheOtherStillReads()
    {
        // The shape that failed on the owner's PC (2026-11-12): a session's source closes while the Advanced reader
        // reads on. Unelevated this cannot show the fault itself - that needs PawnIO and the processor group - so it
        // asserts what LhmLibrary promises of the real Computer: the second user's tree is still there and still
        // updates, the first one's Computer is parked and handed to the next user, and the last close leaves nothing.
        LhmLibrary library = LhmLibrary.Process;
        int usersBefore = library.Users;
        int parkedBefore = library.Parked;
        var reader = new LhmComputerAdapter(LhmGroups.Gpu);
        var session = new LhmComputerAdapter(LhmGroups.Gpu);
        try
        {
            reader.Open();
            session.Open();
            int devices = reader.Hardware.Count;

            session.Close();

            library.Users.Should().Be(usersBefore + 1);
            reader.Invoking(static r => r.Update()).Should().NotThrow("its library was not closed under it");
            reader.Hardware.Should().HaveCount(devices);

            var next = new LhmComputerAdapter(LhmGroups.Gpu);
            next.Open();
            library.Parked.Should().Be(parkedBefore, "the next session took the parked Computer rather than opening another");
            next.Hardware.Should().HaveCount(devices);
            next.Close();
        }
        finally
        {
            session.Close();
            reader.Close();
        }

        library.Users.Should().Be(usersBefore);
        if (usersBefore == 0)
        {
            library.Parked.Should().Be(0, "the last user's close closes every parked Computer with its own");
        }
    }

    [Fact]
    public void TheRealLibraryEitherSamplesOrDisablesAndNeverClaimsAFieldItDidNotRead()
    {
        using var source = new LhmTelemetrySource(new LhmComputerAdapter(enableCpuAndMemory: false),
            new LhmTelemetryOptions(), TimeProvider.System);

        source.Start();
        SpinWait.SpinUntil(() => source.IsDisabled || source.TryRead(out _), TimeSpan.FromSeconds(8));

        if (source.IsDisabled)
        {
            source.Capabilities.Should().Be(GpuCapabilities.None);
            source.LastFault.Should().NotBeNull();
            return;
        }

        if (source.TryRead(out GpuSample? sample))
        {
            // A capability bit is a claim that the field has carried a value; the sample in
            // hand may legitimately have fewer (a null tick), never more.
            (sample.PresentFields & ~source.Capabilities).Should().Be(GpuCapabilities.None);
            sample.Layer.Should().Be(TelemetryLayer.Lhm);
        }
        else
        {
            // No GPU node LHM understands. An answer, not a fault.
            source.Faults.Should().Be(0);
            source.Capabilities.Should().Be(GpuCapabilities.None);
        }
    }
}

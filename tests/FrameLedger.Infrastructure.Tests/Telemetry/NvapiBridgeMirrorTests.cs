// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;
using FluentAssertions;
using FrameLedger.Infrastructure.Telemetry;

namespace FrameLedger.Infrastructure.Tests.Telemetry;

/// <summary>
/// The managed mirrors of <c>FlNvSample</c> and <c>FlNvNgxState</c> against the DLL's own sizes, and the DLL's
/// build id against the guard's — the same mirror discipline <c>ShmLayoutMirrorTests</c> applies, one binary later.
/// Fails rather than skips when the bridge is not staged: a mirror test that quietly does nothing is a gate that cannot fail.
/// </summary>
public sealed class NvapiBridgeMirrorTests
{
    [Fact]
    public void TheMirrorsAreTheSizesTheBridgeAnswersAndTheAbiVersionIsTheOneWeWroteAgainst()
    {
        NativeNvapiBridge.IsPresent.Should().BeTrue("FrameLedger.NvapiBridge.dll must be staged beside the test binary (FrameLedger.NvapiBridge.targets)");

        NativeNvapiBridge.AbiVersion().Should().Be(3u, "ABI 3 added the reads of Advanced metrics (D101); ABI 2 had added FlNvDriverProfile (beta.8)");
        ((uint)Marshal.SizeOf<NvapiSample>()).Should().Be(NativeNvapiBridge.SampleSize());
        ((uint)Marshal.SizeOf<NvapiNgxWords>()).Should().Be(NativeNvapiBridge.NgxStateSize());
        ((uint)Marshal.SizeOf<NvapiDriverProfile>()).Should().Be(NativeNvapiBridge.DriverProfileSize());
        Marshal.SizeOf<NvapiProfileSetting>().Should().Be(24);
        NativeNvapiBridge.BuildId().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TheMirrorsOfAbiThreeAreTheSizesTheBridgeAnswers()
    {
        // A mirror that drifts is refused by the bridge (FL_NV_BAD_SIZE) rather than read as garbage; this is where the
        // drift is seen first, with the struct's name on it. The numbers are fl_nvapi_bridge.h's, spelled out as well.
        NativeNvapiBridge.IsPresent.Should().BeTrue("FrameLedger.NvapiBridge.dll must be staged beside the test binary (FrameLedger.NvapiBridge.targets)");

        ((uint)Marshal.SizeOf<NvapiFacts>()).Should().Be(NativeNvapiBridge.FactsSize()).And.Be(480u);
        ((uint)Marshal.SizeOf<NvapiLive>()).Should().Be(NativeNvapiBridge.LiveSize()).And.Be(120u);
        ((uint)Marshal.SizeOf<NvapiDisplays>()).Should().Be(NativeNvapiBridge.DisplaysSize()).And.Be(400u);
        Marshal.SizeOf<NvapiDisplay>().Should().Be(48);
        ((uint)Marshal.SizeOf<NvapiSessionDisplay>()).Should().Be(NativeNvapiBridge.SessionDisplaySize()).And.Be(32u);
        ((uint)Marshal.SizeOf<NvapiProfileEntry>()).Should().Be(NativeNvapiBridge.ProfileEntrySize()).And.Be(152u);
        ((uint)Marshal.SizeOf<NvapiProfileList>()).Should().Be(NativeNvapiBridge.ProfileListSize()).And.Be(288u);
    }

    [Fact]
    public void TheBitsOfTheManagedEnumsAreTheHeadersBits()
    {
        // The header's FL_NV_FACT_* and FL_NV_LIVE_* are twenty and ten consecutive bits from 1: an enum member added
        // out of order, or one bit skipped, moves every value read after it.
        Enum.GetValues<NvapiFactField>().Where(static f => f != NvapiFactField.None).Select(static f => (uint)f)
            .Should().Equal(Enumerable.Range(0, 20).Select(static i => 1u << i));
        Enum.GetValues<NvapiLiveField>().Where(static f => f != NvapiLiveField.None).Select(static f => (uint)f)
            .Should().Equal(Enumerable.Range(0, 10).Select(static i => 1u << i));
    }

    [Fact]
    public void AnAdvancedReadAnswersAStatusWhateverStateTheBridgeIsInAndNeverThrows()
    {
        // The bridge's state is the PROCESS's, not this instance's: FlNvInit is reference-counted in the DLL, and
        // another test of this assembly may hold a reference while this one runs. So what is held here is what is true
        // in every order - each read answers one of its documented statuses, and a read that did not answer 0 filled
        // nothing. ctest fl_nvapi_bridge, a process of its own, is where "before FlNvInit" is pinned exactly.
        using var bridge = new NativeNvapiBridge();
        int[] documented = [0, -1000, NativeNvapiBridge.NoGpu];

        bridge.ReadFacts(out NvapiFacts facts).Should().BeOneOf(documented, "0, FL_NV_NOT_INITIALISED or FL_NV_NO_GPU");
        int live = bridge.ReadLive(out NvapiLive sample);
        live.Should().BeOneOf(documented);
        if (live != 0)
        {
            sample.Present.Should().Be(0u, "a read that did not answer filled nothing");
        }

        bridge.ReadDisplays(out NvapiDisplays displays).Should().BeOneOf(documented);
        displays.Displays.Should().HaveCount(NvapiDisplays.MaxDisplays);
        facts.Size.Should().Be(480u, "the facade sets the size the bridge checks");

        bridge.SessionDisplay(@"\\.\DISPLAY-THAT-IS-NOT-THERE", 0, out NvapiSessionDisplay session).Should().Be(0, "it never fails for a reason the caller cannot read");
        session.NvapiStatus.Should().NotBe(0, "no display has that name, or NVAPI is not initialised: either way nothing else was asked");
        session.Present.Should().Be(0u);

        bridge.ProfileSettings(exePath: null, ownOnly: false, out NvapiProfileList list, out IReadOnlyList<NvapiProfileEntry> entries).Should().Be(0);
        list.Status.Should().BeOneOf(NvapiDriverProfile.Global, NvapiDriverProfile.Degraded);
        entries.Should().HaveCount((int)list.Count);
        if (list.Status == NvapiDriverProfile.Degraded)
        {
            entries.Should().BeEmpty();
        }
    }

    [Fact]
    public void TheRealBridgeEitherInitialisesOrSaysWhyAndNeverClaimsAFieldItDidNotRead()
    {
        using var bridge = new NativeNvapiBridge();
        using var source = new NvapiTelemetrySource(bridge, TimeProvider.System);

        source.Start();

        if (source.IsDisabled)
        {
            // A runner without an NVIDIA driver, or no GPU: the normal branch on CI.
            source.Faults.Should().Be(0);
            source.LastFault.Should().NotBeNull();
            return;
        }

        source.DriverVersion.Should().MatchRegex(@"^\d+\.\d{2} \(.+\)$");
        source.TryRead(out Application.Telemetry.GpuSample? s).Should().BeTrue();
        s!.AdapterName.Should().NotBeNullOrWhiteSpace();
        (s.PresentFields & ~source.Capabilities).Should().Be(Application.Telemetry.GpuCapabilities.None);
        if (s.LoadPct is { } load)
        {
            load.Should().BeInRange(0, 100);
        }
    }
}

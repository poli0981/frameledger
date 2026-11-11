// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;
using FrameLedger.Shared;

namespace FrameLedger.Application.Tests.Capture;

/// <summary>
/// Region 5 as the Overlay's watchdog would publish it, for the tests of what the Agent makes of it (train B). The
/// values are the ones <c>hook-harness --advanced</c> sends and <c>fl_guard</c> asserts on the native side.
/// </summary>
internal static class InGameStates
{
    public static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);

    /// <summary>A publication that carries nothing: every float "none seen", as the Overlay's accumulators start.</summary>
    public static FlAdvancedState Empty(uint samples = 1) => new()
    {
        Seq = samples * 2,
        Samples = samples,
        Request = 1,
        SampleQpc = samples,
        DlssPreExposureBits = FlAdvancedState.FloatNone,
        DlssExposureScaleBits = FlAdvancedState.FloatNone,
        FfxSharpnessMinBits = FlAdvancedState.FloatNone,
        FfxSharpnessMaxBits = FlAdvancedState.FloatNone,
        FfxJitterMaxXBits = FlAdvancedState.FloatNone,
        FfxJitterMaxYBits = FlAdvancedState.FloatNone,
        FfxPreExposureMinBits = FlAdvancedState.FloatNone,
        FfxPreExposureMaxBits = FlAdvancedState.FloatNone,
        FfxNearBits = FlAdvancedState.FloatNone,
        FfxFarBits = FlAdvancedState.FloatNone,
        FfxFovMinBits = FlAdvancedState.FloatNone,
        FfxFovMaxBits = FlAdvancedState.FloatNone,
    };

    /// <summary>The DLSS options <c>hook-harness --advanced</c> chains into its evaluation, as the vendor's values plus one.</summary>
    public static FlAdvancedState Dlss(uint samples = 1)
    {
        FlAdvancedState s = Empty(samples) with
        {
            ValidMask = FlAdvancedValid.DlssOptions,
            DlssStructVersion = 3,
            DlssOptionsSeen = 60 * samples,
            DlssOutputWidth = 2560,
            DlssOutputHeight = 1440,
            DlssPreExposureBits = Bits(1.5f),
            DlssExposureScaleBits = Bits(0.75f),
            DlssColorBuffersHdr = 1,    // eFalse
            DlssAutoExposure = 2,       // eTrue
            DlssAlphaUpscaling = 2,
        };
        return WithPresets(s, 12, 11, 1, 14, 13, 1);    // DLAA K, Quality J, Balanced default, Performance M, Ultra Performance L, Ultra Quality default
    }

    /// <summary>What <c>hook-harness --advanced --ffx-memory-query</c> sends FSR, one second of it.</summary>
    public static FlAdvancedState Ffx(uint samples = 1) => Empty(samples) with
    {
        ValidMask = FlAdvancedValid.FfxUpscale | FlAdvancedValid.FfxMemory,
        FfxDispatches = 60 * samples,
        FfxSharpenSeen = 2,
        FfxSharpnessMinBits = Bits(0.35f),
        FfxSharpnessMaxBits = Bits(0.35f),
        FfxJitterMaxXBits = Bits(0.3f),
        FfxJitterMaxYBits = Bits(0.4f),
        FfxPreExposureMinBits = Bits(1.25f),
        FfxPreExposureMaxBits = Bits(1.25f),
        FfxNearBits = Bits(0.1f),
        FfxFarBits = Bits(1000f),
        FfxFovMinBits = Bits(0.9f),
        FfxFovMaxBits = Bits(1.1f),
        FfxFlagsSeen = 1u << 2,
        FfxFrameTimeCount = 60,
        FfxFrameTimeSumUs = 60 * 16_000,
        FfxMemoryTotalBytes = 256UL * 1024 * 1024,
        FfxMemoryAliasableBytes = 32UL * 1024 * 1024,
        FfxMemoryQueries = 60 * samples,
    };

    /// <summary>The six preset bytes, DLAA first: a fixed buffer, set through the struct's bytes so no test needs unsafe code.</summary>
    public static FlAdvancedState WithPresets(FlAdvancedState state, params ReadOnlySpan<byte> plusOne)
    {
        int at = (int)Marshal.OffsetOf<FlAdvancedState>(nameof(FlAdvancedState.DlssPresets));
        Span<byte> bytes = MemoryMarshal.AsBytes(new Span<FlAdvancedState>(ref state));
        plusOne.CopyTo(bytes.Slice(at, 6));
        return state;
    }
}

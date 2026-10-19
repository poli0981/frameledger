// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Metrics;

/// <summary>
/// The quality byte a run of params records names — a session's, a segment's or the live card's window — so the three
/// read one rule (beta.25, D65). The most common byte that is not <see cref="FrameSample.QualityNotTold"/>, a tie going
/// to the lower; and for FSR's bytes, none when the game rendered at a size no FSR-named record has.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why FSR's byte needs the second condition.</b> FSR's dispatch carries no mode: the Overlay names one per frame
/// only when the size the frame was upscaled from is the size the vendor's query gave for that mode (<c>dllmain.cpp</c>
/// <c>FfxQualityOf</c>). At a dynamic resolution most frames are at sizes no answer gave and carry "not told", while the
/// frames at the top of the range still match — so the plain modal would name a mode for a session that mostly did not
/// render at it. <c>legal/ACCURACY.md</c> promises the opposite: "FSR at a dynamic resolution among them — no mode is
/// shown". An FSR record at a size no named record has is that evidence, and it withholds the mode.
/// </para>
/// <para>
/// <b>What is not evidence.</b> A "not told" record at a named size — a present that drained a frame-generation PREPARE
/// alone, or a frame before the title asked — and a record of another upscaler (its sizes are its own).
/// </para>
/// </remarks>
public static class QualityByte
{
    /// <summary>FSR's NativeAA: <c>0x10</c> plus <c>FFX_UPSCALE_QUALITY_MODE_NATIVEAA</c> (<c>fl_shm.h</c>).</summary>
    public const byte FsrFirst = 0x10;

    /// <summary>FSR's Ultra Performance: <c>0x10</c> plus <c>FFX_UPSCALE_QUALITY_MODE_ULTRA_PERFORMANCE</c>.</summary>
    public const byte FsrLast = 0x14;

    /// <summary>Whether a byte is one of FSR's five.</summary>
    public static bool IsFsr(byte value) => value is >= FsrFirst and <= FsrLast;

    /// <summary>The byte the records name, or null when none is told or an FSR mode is withheld.</summary>
    /// <param name="withParams">The records that claim the params bit; the caller filters, as it does today.</param>
    public static byte? Of(IReadOnlyList<FrameSample> withParams)
    {
        ArgumentNullException.ThrowIfNull(withParams);
        byte? modal = withParams
            .Where(static s => s.UpscalerQuality != FrameSample.QualityNotTold)
            .GroupBy(static s => s.UpscalerQuality)
            .OrderByDescending(static g => g.Count())
            .ThenBy(static g => g.Key)
            .Select(static g => (byte?)g.Key)
            .FirstOrDefault();
        if (modal is not { } named || !IsFsr(named))
        {
            return modal;
        }

        HashSet<(ushort W, ushort H)> namedSizes = [.. withParams.Where(static s => IsFsr(s.UpscalerQuality)).Select(static s => (s.RenderW, s.RenderH))];
        return withParams.Any(s => IsFsrRecord(s) && !namedSizes.Contains((s.RenderW, s.RenderH))) ? null : named;
    }

    private static bool IsFsrRecord(FrameSample s) =>
        s.Claims(MeasuredFields.Upscaler) && s.Upscaler is UpscalerKind.Fsr2 or UpscalerKind.Fsr3 or UpscalerKind.Fsr4 or UpscalerKind.FsrUnversioned;
}

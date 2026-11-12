// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Metrics;

/// <summary>
/// Which upscaler a stream of samples names, and the samples its quality and render size are to be read from - so
/// that the name, the quality and the size a session shows are those of ONE technology (owner decision D120,
/// 2026-11-12; <c>03_METRICS</c> §Upscaling).
/// </summary>
/// <remarks>
/// <para>
/// <b>WHY.</b> The session's name used to be the FIRST technology any record named, while its quality byte and its
/// render size came from whichever records carried them. A real session of 81 seconds (Dying Light: The Beast, an FSR
/// dispatch and a DLSS evaluation both seen) was stored as "DLSS" with FSR's quality byte and FSR's render size: three
/// true readings, of two technologies, under one name.
/// </para>
/// <para>
/// <b>THE RULE.</b> The technology named on the most samples; a tie goes to the lower value, as
/// <see cref="QualityByte.Of"/> breaks its own. While only one technology is named the samples are the whole stream,
/// exactly as before - under frame generation a name sits on one present in N, and the render size of the presents
/// between must still count. When more than one is named, the samples are those that name the chosen one, and
/// nothing of the other is carried under its name.
/// </para>
/// </remarks>
public sealed record UpscalerChoice
{
    private UpscalerChoice(UpscalerKind? kind, int named, IReadOnlyList<FrameSample> samples)
    {
        Kind = kind;
        Named = named;
        Samples = samples;
    }

    /// <summary>The technology named on the most samples; null when no sample names one.</summary>
    public UpscalerKind? Kind { get; }

    /// <summary>How many different technologies the stream names. More than one is a setting changed mid-session.</summary>
    public int Named { get; }

    /// <summary>The samples the quality and the render size are read from.</summary>
    public IReadOnlyList<FrameSample> Samples { get; }

    /// <summary>True for a value that names a technology: not "not reported", not "unknown", not the retired value.</summary>
    public static bool IsNamed(UpscalerKind kind) =>
        kind is not (UpscalerKind.NotReported or UpscalerKind.Unknown or UpscalerKind.RetiredRayReconstruction);

    /// <summary>True for FSR in any of its versions, the family <see cref="QualityByte.IsFsr"/>'s bytes belong to.</summary>
    public static bool IsFsr(UpscalerKind kind) =>
        kind is UpscalerKind.Fsr2 or UpscalerKind.Fsr3 or UpscalerKind.Fsr4 or UpscalerKind.FsrUnversioned;

    /// <summary>
    /// A quality byte that belongs to the named technology's family, or null: FSR's bytes under an FSR name, any
    /// other byte under any other name. A byte of the other family is withheld rather than shown under a name it
    /// was not measured for.
    /// </summary>
    public byte? QualityFor(byte? quality) =>
        quality is byte value && Kind is { } kind && QualityByte.IsFsr(value) != IsFsr(kind) ? null : quality;

    public static UpscalerChoice From(IReadOnlyList<FrameSample> stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        (UpscalerKind Kind, int Count)[] named =
        [
            .. stream
                .Where(static s => s.Claims(MeasuredFields.Upscaler) && IsNamed(s.Upscaler))
                .GroupBy(static s => s.Upscaler)
                .Select(static g => (g.Key, g.Count()))
                .OrderByDescending(static g => g.Item2)
                .ThenBy(static g => g.Key),
        ];
        if (named.Length == 0)
        {
            return new UpscalerChoice(kind: null, named: 0, stream);
        }

        UpscalerKind chosen = named[0].Kind;
        return named.Length == 1
            ? new UpscalerChoice(chosen, named: 1, stream)
            : new UpscalerChoice(chosen, named.Length, [.. stream.Where(s => s.Claims(MeasuredFields.Upscaler) && s.Upscaler == chosen)]);
    }
}

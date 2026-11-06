// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Metrics;

/// <summary>
/// DLSS 5's neural rendering in a session (beta.34, owner decision D84; <c>05_DETECTION</c> §DLSS generation): on how many
/// of the frames the game rendered through Streamline it evaluated <c>kFeatureDLSS_NR</c>.
/// </summary>
/// <remarks>
/// Counted over the presents that drained a Streamline evaluation (<see cref="FeatureBits.RayReconstructionObserved"/>, the
/// one condition every Streamline fact of the byte is published under) — about one per application frame, so a session
/// under frame generation is not diluted by its generated presents. A stream with no such present gives no share at all:
/// the title never routed through Streamline while the hook was in, and its silence says nothing about DLSS 5. Whether the
/// writer could have seen neural rendering is the caller's to ask first (<see cref="HookFamilies.SlNeuralRendering"/>): an
/// Overlay older than beta.34 drains the same batches and never sets the bit.
/// </remarks>
public static class NeuralRendering
{
    /// <summary>The percentage (0–100) of the stream's Streamline batches that carried a neural-rendering evaluation, or null.</summary>
    public static double? SharePct(IReadOnlyList<FrameSample> stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        int batches = 0;
        int neural = 0;
        foreach (FrameSample s in stream)
        {
            if (!s.Features.HasFlag(FeatureBits.RayReconstructionObserved))
            {
                continue;
            }

            batches++;
            if (s.Features.HasFlag(FeatureBits.SlNeuralRendering))
            {
                neural++;
            }
        }

        return batches == 0 ? null : 100.0 * neural / batches;
    }
}

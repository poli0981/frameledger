// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Metrics;
using static FrameLedger.Domain.Tests.Metrics.SampleFixtures;

namespace FrameLedger.Domain.Tests.Metrics;

/// <summary>
/// beta.34, owner decision D84: DLSS 5's neural rendering, as the share of the presents that drained a Streamline batch —
/// the frames the game rendered through Streamline — on which <c>kFeatureDLSS_NR</c> was evaluated.
/// </summary>
public sealed class NeuralRenderingTests
{
    [Fact]
    public void TheShareIsOverTheBatchesAndNothingDrainedIsNoShare()
    {
        FrameSample plain = Present(1);
        FrameSample batch = plain with { Features = FeatureBits.RayReconstructionObserved };
        FrameSample neural = plain with { Features = FeatureBits.RayReconstructionObserved | FeatureBits.SlNeuralRendering };

        NeuralRendering.SharePct([plain, plain]).Should().BeNull("no present drained a Streamline batch: silence says nothing");
        NeuralRendering.SharePct([]).Should().BeNull();
        NeuralRendering.SharePct([batch, batch, plain]).Should().Be(0, "Streamline batches, and none evaluated neural rendering");
        NeuralRendering.SharePct([neural, neural, neural, batch]).Should().Be(75);
        NeuralRendering.SharePct([neural, plain, neural, plain, neural, plain]).Should().Be(100,
            "a generated present drains no batch, so frame generation does not dilute the share");
        NeuralRendering.SharePct([plain with { Features = FeatureBits.SlNeuralRendering }]).Should().BeNull(
            "the fact without its OBSERVED says nothing");
        FluentActions.Invoking(static () => NeuralRendering.SharePct(null!)).Should().Throw<ArgumentNullException>();
    }
}

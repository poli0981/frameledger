// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Advanced;
using FrameLedger.Application.Recording;

namespace FrameLedger.Application.Tests.Recording;

/// <summary>
/// What a hooked game published and no sample carries yet (owner decision D150): two sets may wait, the older is
/// carried first, and a set that waited is no longer overwritten by the one after it.
/// </summary>
public sealed class PendingInGameReadingsTests
{
    private static IReadOnlyList<AdvancedReading> Set(int second) => [new AdvancedReading("adv.nvapi.reflex.frame_report.total", second)];

    private static double Second(IReadOnlyList<AdvancedReading> set) => set[0].Value;

    [Fact]
    public void Nothing_waits_until_something_is_offered()
    {
        var pending = new PendingInGameReadings();

        pending.Count.Should().Be(0);
        pending.TryTake(out IReadOnlyList<AdvancedReading>? none).Should().BeFalse();
        none.Should().BeNull();
    }

    [Fact]
    public void Two_publications_between_two_samples_are_both_carried_the_older_first()
    {
        var pending = new PendingInGameReadings();
        pending.Offer(Set(1));
        pending.Offer(Set(2));

        pending.Count.Should().Be(2);
        pending.TryTake(out IReadOnlyList<AdvancedReading>? first).Should().BeTrue();
        Second(first!).Should().Be(1);
        pending.TryTake(out IReadOnlyList<AdvancedReading>? second).Should().BeTrue();
        Second(second!).Should().Be(2);
        pending.TryTake(out _).Should().BeFalse();
    }

    [Fact]
    public void A_third_pushes_the_oldest_out_so_a_slow_sensor_interval_keeps_the_two_newest()
    {
        var pending = new PendingInGameReadings();
        pending.Offer(Set(1));
        pending.Offer(Set(2));
        pending.Offer(Set(3));

        pending.Count.Should().Be(PendingInGameReadings.Capacity);
        pending.TryTake(out IReadOnlyList<AdvancedReading>? first).Should().BeTrue();
        Second(first!).Should().Be(2);
        pending.TryTake(out IReadOnlyList<AdvancedReading>? second).Should().BeTrue();
        Second(second!).Should().Be(3);
    }

    [Fact]
    public void A_publication_with_no_reading_does_not_wait()
    {
        var pending = new PendingInGameReadings();
        pending.Offer(Set(1));
        pending.Offer([]);

        pending.Count.Should().Be(1);
        pending.TryTake(out IReadOnlyList<AdvancedReading>? first).Should().BeTrue();
        Second(first!).Should().Be(1);
    }

    [Fact]
    public void A_null_set_is_refused()
    {
        var pending = new PendingInGameReadings();

        Action offer = () => pending.Offer(null!);

        offer.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// The owner's sessions, as the recorder sees them. Each second has one publication and one sample. In step, one
    /// look of the recorder finds both - the publication is taken first, the sample carries it. Out of step, the
    /// publication lands just after the sample: a look falls between them, so the sample is drained before its
    /// publication is taken. The phase wanders, so a session goes in and out of step.
    /// </summary>
    [Fact]
    public void A_session_that_falls_out_of_step_and_back_lacks_one_sample_and_loses_no_reading()
    {
        var pending = new PendingInGameReadings();
        var carried = new List<double?>();
        void Sample() => carried.Add(pending.TryTake(out IReadOnlyList<AdvancedReading>? set) ? Second(set) : null);

        int second = 0;
        for (int round = 0; round < 3; round++)
        {
            for (int i = 0; i < 4; i++)
            {
                // In step: the publication, then the sample, in one look.
                pending.Offer(Set(++second));
                Sample();
            }

            for (int i = 0; i < 4; i++)
            {
                // Out of step: the sample is drained in one look, its publication is taken in the next.
                Sample();
                pending.Offer(Set(++second));
            }
        }

        // Twenty-four seconds, twenty-four samples: one without a reading - the first that fell out of step - and
        // every reading up to the last carried once, in order. The last one waits for a sample that never came.
        carried.Should().HaveCount(24);
        carried.Count(static c => c is null).Should().Be(1);
        carried[4].Should().BeNull();
        carried.Where(static c => c is not null).Should().Equal(Enumerable.Range(1, 23).Select(static s => (double?)s));
        pending.Count.Should().Be(1);
    }

    /// <summary>The rule until 0.1.0-beta.49, on the same session: what the change is for.</summary>
    [Fact]
    public void The_rule_before_it_lacked_a_sample_at_every_fall_out_of_step_and_lost_a_reading_at_every_return()
    {
        IReadOnlyList<AdvancedReading>? newest = null;
        var carried = new List<double?>();
        void Sample()
        {
            carried.Add(newest is null ? null : Second(newest));
            newest = null;
        }

        int second = 0;
        for (int round = 0; round < 3; round++)
        {
            for (int i = 0; i < 4; i++)
            {
                newest = Set(++second);
                Sample();
            }

            for (int i = 0; i < 4; i++)
            {
                Sample();
                newest = Set(++second);
            }
        }

        carried.Count(static c => c is null).Should().Be(3);
        carried.Where(static c => c is not null).Should().HaveCount(21).And.NotContain(8d).And.NotContain(16d);
    }
}

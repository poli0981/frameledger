// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.Application.Persistence;
using FrameLedger.Infrastructure.Blobs;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.23 (<c>spike-notes</c> §21): what the charts derive from a whole session is worked out once, off the UI thread, and
/// it is exactly what each chart computed for itself before — one sort where there were three.
/// </summary>
public sealed class SeriesDigestTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Frame times with what a real stream has: zeros after gaps, ties, a long tail, one negative a corrupt blob could hold.</summary>
    private static double[] Noisy(int count, int seed)
    {
        // A fixed scramble, the same on every run (a Random would be CA5394 here and adds nothing).
        uint state = (uint)seed | 1;
        var times = new double[count];
        for (int i = 0; i < count; i++)
        {
            state = (state * 1_664_525) + 1_013_904_223;
            double jitter = (state >> 8) / (double)(1 << 24);
            times[i] = i % 97 == 0 ? 0 : i % 211 == 0 ? 6.25 : 4 + (jitter * 3) + (i % 503 == 0 ? 40 : 0);
        }

        times[count / 2] = -1;
        return times;
    }

    [Theory]
    [InlineData(2)]
    [InlineData(1_000)]
    [InlineData(25_000)]
    public void TheSortedFormsAreTheSameAsTheChartsOwn(int count)
    {
        double[] times = Noisy(count, count);
        double[] sorted = (double[])times.Clone();
        Array.Sort(sorted);

        FpsHistogram own = FpsHistogram.Of(times, SeriesDigest.HistogramBins);
        FpsHistogram fromSorted = FpsHistogram.OfSorted(sorted, SeriesDigest.HistogramBins);
        fromSorted.Centers.Should().Equal(own.Centers);
        fromSorted.Counts.Should().Equal(own.Counts);
        fromSorted.BinWidth.Should().Be(own.BinWidth);
        fromSorted.Outside.Should().Be(own.Outside);

        (double[] xs, double[] ys) = PercentileCurve.Of(times);
        (double[] sortedXs, double[] sortedYs) = PercentileCurve.OfSorted(sorted);
        sortedXs.Should().Equal(xs);
        sortedYs.Should().Equal(ys);
        PercentileCurve.LowsOfSorted(sorted).Should().Equal(PercentileCurve.Lows(times));
    }

    [Fact]
    public void OneRateAndNoFramesStayWhatTheyWere()
    {
        double[] flat = [5, 5, 5, 5];
        FpsHistogram.OfSorted(flat).Should().BeEquivalentTo(FpsHistogram.Of(flat));
        FpsHistogram.OfSorted([0, 0]).Should().BeEquivalentTo(FpsHistogram.Of([0, 0]));
        FpsHistogram.OfSorted([]).Centers.Should().BeEmpty();
        PercentileCurve.OfSorted([7]).Xs.Should().BeEmpty();
    }

    [Fact]
    public void TheDecodedSeriesCarriesItsDigest()
    {
        // 6 presents: the first and the fifth follow a gap, the third is generated.
        float[] frametimes = [0, 10, 10, 10, 0, 40];
        byte[] flags = [4, 0, 1, 0, 4, 0];
        var frames = new FrameBlobs
        {
            Codec = SeriesCodec.Tag,
            SampleCount = 6,
            FrameTimes = SeriesCodec.EncodeFloat32(frametimes),
            FrameFlags = SeriesCodec.EncodeBytes(flags),
        };

        SessionSeries s = SessionSeriesLoader.Decode(frames, [], []);

        SeriesDigest digest = s.Digest!;
        digest.SortedAppFrameTimesMs.Should().Equal(10, 20, 40);
        digest.GapsAfterFirst.Should().Be(1, "the first present's gap is every stream's start, not one the chart notes");
        digest.GeneratedCount.Should().Be(1);
        s.HasGenerated.Should().BeTrue();
        (double[] presentTimes, double[] presentIntervals) = digest.Presents!.Value;
        presentTimes.Should().Equal(0.01, 0.02, 0.03, 0.07);
        presentIntervals.Should().Equal(10, 10, 10, 40);
        FrametimeChart.NoteFor(s, displayed: false, sensors: false).Should().Be(FrametimeChart.NoteFor(s with { Digest = null }, displayed: false, sensors: false));
    }

    /// <summary>
    /// Microsoft.Data.Sqlite completes its "async" calls synchronously, so whatever ConfigureAwait said, the blob read and the
    /// decode ran on the caller's thread — the UI's (beta.23). They run on the thread pool now.
    /// </summary>
    [Fact]
    public async Task TheBlobIsReadAndDecodedOffTheCallersThread()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        long id = await s.SessionWithFramesAsync(game.Id, DateTimeOffset.UtcNow.AddHours(-1), frames: 600, spikeEvery: 200);
        var reads = new ThreadRecordingSessions(s.Sessions);
        var loader = new SessionSeriesLoader(reads);

        int caller = 0;
        Task<SessionSeries?>? loading = null;
        var thread = new Thread(() =>
        {
            caller = Environment.CurrentManagedThreadId;
            loading = loader.LoadAsync(id, Ct);
        });
        thread.Start();
        thread.Join();
        SessionSeries? series = await loading!;

        series.Should().NotBeNull();
        reads.Threads.Should().NotBeEmpty().And.NotContain(caller, "the read and the decode happen on the thread pool, never on the thread that asked");
    }

    /// <summary>The scratch ledger's repository, noting the thread each blob read runs on.</summary>
    private sealed class ThreadRecordingSessions(ISessionRepository inner) : ISessionRepository
    {
        public List<int> Threads { get; } = [];

        public ValueTask<FrameBlobs?> FindFramesAsync(long sessionId, CancellationToken ct = default)
        {
            lock (Threads)
            {
                Threads.Add(Environment.CurrentManagedThreadId);
            }

            return inner.FindFramesAsync(sessionId, ct);
        }

        public ValueTask<IReadOnlyList<SegmentRow>> FindSegmentsAsync(long sessionId, CancellationToken ct = default) => inner.FindSegmentsAsync(sessionId, ct);

        public ValueTask<IReadOnlyList<SensorBlob>> FindSensorsAsync(long sessionId, CancellationToken ct = default) => inner.FindSensorsAsync(sessionId, ct);

        public ValueTask<long> InsertFinalizedAsync(FinalizedSession session, CancellationToken ct = default) => inner.InsertFinalizedAsync(session, ct);

        public ValueTask<long?> ResolveOwnerAsync(long gameId, string? exePath, DateTimeOffset startedAt, CancellationToken ct = default) => inner.ResolveOwnerAsync(gameId, exePath, startedAt, ct);

        public ValueTask<bool> ExistsAsync(Guid sessionGuid, CancellationToken ct = default) => inner.ExistsAsync(sessionGuid, ct);

        public ValueTask<SessionRow?> FindAsync(Guid sessionGuid, CancellationToken ct = default) => inner.FindAsync(sessionGuid, ct);

        public ValueTask<SessionRow?> FindByIdAsync(long sessionId, CancellationToken ct = default) => inner.FindByIdAsync(sessionId, ct);

        public ValueTask<IReadOnlyList<SessionRow>> ListRecentAsync(int count, CancellationToken ct = default) => inner.ListRecentAsync(count, ct);

        public ValueTask<IReadOnlyList<SessionRow>> ListByGameAsync(long gameId, int limit, CancellationToken ct = default) => inner.ListByGameAsync(gameId, limit, ct);

        public ValueTask<IReadOnlyList<GameSessionSummary>> SummariseByGameAsync(CancellationToken ct = default) => inner.SummariseByGameAsync(ct);

        public ValueTask<long> CountSinceAsync(DateTimeOffset since, CancellationToken ct = default) => inner.CountSinceAsync(since, ct);

        public ValueTask<int> CountSuccessfulUnderGrantAsync(long gameId, string family, DateTimeOffset grantedAt, CancellationToken ct = default) => inner.CountSuccessfulUnderGrantAsync(gameId, family, grantedAt, ct);

        public ValueTask<int> SweepRetentionAsync(long gameId, int keep, CancellationToken ct = default) => inner.SweepRetentionAsync(gameId, keep, ct);

        public ValueTask<RetentionSweepResult> SweepRetentionAllAsync(int keep, CancellationToken ct = default) => inner.SweepRetentionAllAsync(keep, ct);

        public ValueTask<RetentionSweepResult> DeleteSessionsAsync(long? gameId, CancellationToken ct = default) => inner.DeleteSessionsAsync(gameId, ct);
    }
}

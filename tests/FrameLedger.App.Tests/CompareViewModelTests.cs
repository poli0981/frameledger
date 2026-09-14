using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Tests;

/// <summary>Compare over a scratch ledger: the picker's bounds, FR-6.2's guard gating a mixed selection, the curves for hooked sessions only, the table's best-value rule and its N/A cells.</summary>
public sealed class CompareViewModelTests
{
    private sealed class ScriptedMixed(bool answer) : IMixedTierPrompt
    {
        public int Asked { get; private set; }

        public Task<bool> AcknowledgeAsync(CancellationToken ct = default)
        {
            Asked++;
            return Task.FromResult(answer);
        }
    }

    private sealed class NoSaver : IFileSaver
    {
        public string? PickSavePath(string filter, string suggestedName) => null;
    }

    private sealed class NoStrip : IMessageStrip
    {
        public void Info(string title, string body)
        {
        }

        public void Success(string title, string body)
        {
        }

        public void Warn(string title, string body)
        {
        }
    }

    private static async Task<CompareViewModel> BuildAsync(ScratchLedger s, ScriptedMixed mixed)
    {
        var vm = new CompareViewModel(s.Library, new SessionSeriesLoader(s.Sessions), mixed, new NoSaver(), new NoStrip());
        Task pending = vm.Pending;
        await pending.ConfigureAwait(false);
        return vm;
    }

    [Fact]
    public async Task ThePickerNeedsTwoToFiveAndAMixedSelectionIsAQuestionFirst()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow a = await s.GameAsync("Alpha");
        GameRow b = await s.GameAsync("Beta");
        await s.SessionWithFramesAsync(a.Id, DateTimeOffset.UtcNow.AddHours(-3), frames: 300, spikeEvery: 0);
        await s.SessionWithFramesAsync(b.Id, DateTimeOffset.UtcNow.AddHours(-2), frames: 300, spikeEvery: 50);
        await s.SessionAsync(a.Id, DateTimeOffset.UtcNow.AddHours(-1), hooked: false);
        var mixed = new ScriptedMixed(answer: false);
        CompareViewModel vm = await BuildAsync(s, mixed);

        vm.IsEmpty.Should().BeFalse();
        vm.Candidates.Should().HaveCount(3);
        vm.CanCompare.Should().BeFalse("nothing picked");
        vm.Candidates[0].IsSelected = true;
        vm.CanCompare.Should().BeFalse("one is not a comparison");
        vm.Candidates[1].IsSelected = true;
        vm.CanCompare.Should().BeTrue();
        vm.SelectedCount.Should().Be(2);

        // a hooked + a not-hooked selection: the guard is asked, and a declined guard compares nothing
        vm.Candidates[1].IsSelected = false;   // the Tier-2 one (newest, index 0) stays picked, now with Alpha's hooked one
        vm.Candidates[2].IsSelected = true;
        vm.CompareCommand.Execute(null);
        Task pending = vm.Pending;
        await pending;
        mixed.Asked.Should().Be(1);
        vm.HasResult.Should().BeFalse("FR-6.2: no acknowledgement, no comparison");
    }

    [Fact]
    public async Task AnAcknowledgedMixedComparisonCarriesTheLegendAndKeepsTierTwoAtNa()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            await using ScratchLedger s = await ScratchLedger.OpenAsync();
            GameRow a = await s.GameAsync("Alpha");
            await s.SessionWithFramesAsync(a.Id, DateTimeOffset.UtcNow.AddHours(-3), frames: 300, spikeEvery: 0);
            await s.SessionWithFramesAsync(a.Id, DateTimeOffset.UtcNow.AddHours(-2), frames: 300, spikeEvery: 50);
            await s.SessionAsync(a.Id, DateTimeOffset.UtcNow.AddHours(-1), hooked: false);
            var mixed = new ScriptedMixed(answer: true);
            CompareViewModel vm = await BuildAsync(s, mixed);
            foreach (CompareCandidateViewModel c in vm.Candidates)
            {
                c.IsSelected = true;
            }

            vm.CompareCommand.Execute(null);
            Task pending = vm.Pending;
            await pending;

            vm.HasResult.Should().BeTrue();
            vm.IsMixed.Should().BeTrue();
            vm.TierLegend.Should().Be(Strings.Compare_Mixed_Legend);
            vm.Curves.Should().HaveCount(2, "a Tier-2 session has no frames and no curve");
            vm.Compared.Should().HaveCount(3);

            CompareRowViewModel median = vm.Rows.Single(r => string.Equals(r.Metric, "Median", StringComparison.Ordinal));
            median.Cells[0].Text.Should().Be("N/A", "the newest, Tier 2, is first in the recent order");
            median.Cells[0].IsBest.Should().BeFalse("N/A is never best");
            median.Cells.Count(static c => c.IsBest).Should().Be(2, "both hooked sessions stored the same median, and a tie is best on both");
            CompareRowViewModel temp = vm.Rows.Single(r => string.Equals(r.Metric, "Max GPU °C", StringComparison.Ordinal));
            temp.Cells.Count(static c => c.IsBest).Should().BeGreaterThan(0, "lower is better and every row has 71 °C, so the ties are all best");
            CompareRowViewModel presented = vm.Rows.Single(r => string.Equals(r.Metric, "Presented FPS", StringComparison.Ordinal));
            presented.Cells.Should().OnlyContain(static c => c.Text == "N/A", "these sessions measured FG as none, so the Native row has them and this one does not");
            CompareRowViewModel native = vm.Rows.Single(r => string.Equals(r.Metric, "Native FPS", StringComparison.Ordinal));
            native.Cells.Skip(1).Should().OnlyContain(static c => c.Text == "60");
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    [Fact]
    public void TheBestOfARowIsTheHighestOrTheLowestAndNeverAnNa()
    {
        SessionRow[] rows =
        [
            new() { SessionGuid = Guid.NewGuid(), GameId = 1, SnapshotId = 1, StartedAt = DateTimeOffset.UnixEpoch, EndedAt = DateTimeOffset.UnixEpoch, QpcEpoch = 0, QpcFrequency = 1, Tier = Domain.Sessions.CaptureTier.Hooked, Mode = Domain.Sessions.CaptureMode.Launch, ExitStatus = Domain.Sessions.ExitStatus.Normal, MaxGpuTemp = 80 },
            new() { SessionGuid = Guid.NewGuid(), GameId = 1, SnapshotId = 1, StartedAt = DateTimeOffset.UnixEpoch, EndedAt = DateTimeOffset.UnixEpoch, QpcEpoch = 0, QpcFrequency = 1, Tier = Domain.Sessions.CaptureTier.Hooked, Mode = Domain.Sessions.CaptureMode.Launch, ExitStatus = Domain.Sessions.ExitStatus.Normal, MaxGpuTemp = 65 },
            new() { SessionGuid = Guid.NewGuid(), GameId = 1, SnapshotId = 1, StartedAt = DateTimeOffset.UnixEpoch, EndedAt = DateTimeOffset.UnixEpoch, QpcEpoch = 0, QpcFrequency = 1, Tier = Domain.Sessions.CaptureTier.NotHooked, Mode = Domain.Sessions.CaptureMode.Launch, ExitStatus = Domain.Sessions.ExitStatus.Normal, MaxGpuTemp = null },
        ];

        CompareRowViewModel lower = CompareViewModel.Row("t", rows, static r => r.MaxGpuTemp, Formats.Temperature, higherIsBetter: false);
        lower.Cells.Select(static c => c.IsBest).Should().Equal(false, true, false);
        CompareRowViewModel higher = CompareViewModel.Row("t", rows, static r => r.MaxGpuTemp, Formats.Temperature, higherIsBetter: true);
        higher.Cells.Select(static c => c.IsBest).Should().Equal(true, false, false);
        CompareRowViewModel none = CompareViewModel.Row("t", rows, static _ => null, Formats.Temperature, higherIsBetter: true);
        none.Cells.Should().OnlyContain(static c => !c.IsBest);
    }
}

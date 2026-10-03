// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.App.Tests;

/// <summary>
/// The game's own memory as the pages state it (beta.12, D43): Task Manager's units, the median with the peak beneath, the
/// working set LABELLED where the private working set is N/A, the process count where an unpinned hold summed several,
/// N/A for a session recorded before — and the per-series statistics table in each series' own unit.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class GameMemoryTextTests
{
    private static T InEnglish<T>(Func<T> f)
    {
        CultureInfo? previous = Strings.Culture;
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            Strings.Culture = CultureInfo.GetCultureInfo("en");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
            return f();
        }
        finally
        {
            Strings.Culture = previous;
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private static SessionRow Row(bool hooked = true) => new()
    {
        SessionGuid = Guid.NewGuid(),
        GameId = 1,
        SnapshotId = 1,
        StartedAt = DateTimeOffset.UnixEpoch,
        EndedAt = DateTimeOffset.UnixEpoch.AddMinutes(10),
        QpcEpoch = 0,
        QpcFrequency = 1,
        Tier = hooked ? CaptureTier.Hooked : CaptureTier.NotHooked,
        Mode = CaptureMode.Launch,
        ExitStatus = ExitStatus.Normal,
    };

    /// <summary>A session that read everything: 6.5 GiB dedicated at the median, 7 GiB peak, 512 MiB shared, 3 GiB private working set.</summary>
    private static SessionRow Measured(bool hooked = true) => Row(hooked) with
    {
        GameVramDedicatedAvgMb = 6400,
        GameVramDedicatedMedianMb = 6656,
        GameVramDedicatedMaxMb = 7168,
        GameVramSharedMaxMb = 512,
        GameRamPrivateAvgMb = 3000,
        GameRamPrivateMedianMb = 3072,
        GameRamPrivateMaxMb = 3584,
        GameRamWorkingSetMaxMb = 4096,
        GameCommitMaxMb = 5120,
        GameMemoryProcesses = 1,
        GameMemorySource = "counters,ex2,held",
    };

    private static readonly Dictionary<string, SensorSeriesStats> _noStats = new(StringComparer.Ordinal);

    [Fact]
    public void MemoryIsStatedTheWayTaskManagerStatesItAndNeverAsZero()
    {
        InEnglish(() => Formats.Memory(null)).Should().Be("N/A");
        InEnglish(() => Formats.Memory(0)).Should().Be("0 MB", "a zero that was READ is a reading");
        InEnglish(() => Formats.Memory(812.4)).Should().Be("812 MB");
        InEnglish(() => Formats.Memory(1023.4)).Should().Be("1,023 MB");
        InEnglish(() => Formats.Memory(1023.6)).Should().Be("1.00 GB", "never \"1,024 MB\"");
        InEnglish(() => Formats.Memory(1024)).Should().Be("1.00 GB", "1 GB = 1024 MB, Windows' own units");
        InEnglish(() => Formats.Memory(6656)).Should().Be("6.50 GB");
        InEnglish(() => Formats.Power(214.6)).Should().Be("215 W");
        InEnglish(() => Formats.Power(null)).Should().Be("N/A");
    }

    [Fact]
    public void TheCardsShowTheMedianWithThePeakBeneathInEitherTier()
    {
        foreach (bool hooked in new[] { true, false })
        {
            StatCardModel vram = InEnglish(() => GameMemoryText.VramCard(Measured(hooked)));
            vram.Label.Should().Be("VRAM (this game)");
            vram.Value.Should().Be("6.50 GB");
            vram.Suffix.Should().Be("median · peak 7.00 GB · shared 512 MB");

            StatCardModel ram = InEnglish(() => GameMemoryText.RamCard(Measured(hooked), _noStats));
            ram.Label.Should().Be("RAM (this game)");
            ram.Value.Should().Be("3.00 GB");
            ram.Suffix.Should().Be("median · peak 3.50 GB · commit 5.00 GB");
        }
    }

    [Fact]
    public void ASessionRecordedBeforeBetaTwelveReadsNotAvailableWithNothingBeneath()
    {
        StatCardModel vram = InEnglish(() => GameMemoryText.VramCard(Row()));
        vram.Value.Should().Be("N/A");
        vram.Suffix.Should().BeNull();
        InEnglish(() => GameMemoryText.RamCard(Row(), _noStats)).Value.Should().Be("N/A");
        InEnglish(() => GameMemoryText.VramCell(Row())).Should().Be("N/A", "never \"N/A · N/A\"");
        InEnglish(() => GameMemoryText.RamCell(Row(), _noStats)).Should().Be("N/A");
        GameMemoryText.VramTooltip(Row()).Should().BeNull();
        GameMemoryText.RamTooltip(Row(), _noStats).Should().BeNull();
        GameMemoryText.ExportLine(Row()).Should().Be("N/A");
        GameMemoryText.Live(null, null).Should().BeEmpty();
    }

    /// <summary>
    /// A Windows without the September 2023 update reports no private working set: the working set takes its place and says
    /// so — on the card, in the cell and in the tooltip — never passed off as Task Manager's "Memory".
    /// </summary>
    [Fact]
    public void WithoutThePrivateWorkingSetTheWorkingSetIsShownAndSaysSo()
    {
        SessionRow row = Row() with { GameRamWorkingSetMaxMb = 4096, GameCommitMaxMb = 5120 };
        var stats = new Dictionary<string, SensorSeriesStats>(StringComparer.Ordinal)
        {
            [SensorSeriesCatalog.GameRamWorkingSet] = new SensorSeriesStats(600, 3900, 3840, 1024, 4096),
        };

        StatCardModel card = InEnglish(() => GameMemoryText.RamCard(row, stats));
        card.Label.Should().Be("RAM (this game, working set)");
        card.Value.Should().Be("3.75 GB", "the working set's STORED median");
        card.Suffix.Should().Contain("does not report the private working set");
        InEnglish(() => GameMemoryText.RamCell(row, stats)).Should().Be("3.75 GB · 4.00 GB (working set)");
        InEnglish(() => GameMemoryText.RamTooltip(row, stats)).Should().StartWith("This Windows does not report the private working set");
    }

    [Fact]
    public void AnUnpinnedHoldThatSummedSeveralProcessesSaysHowMany()
    {
        SessionRow row = Measured(hooked: false) with { GameMemoryProcesses = 3, GameMemorySource = "counters,ex2,opened" };

        InEnglish(() => GameMemoryText.VramCard(row)).Suffix.Should().EndWith("3 processes running the game's executable, added together");
        InEnglish(() => GameMemoryText.RamCard(row, _noStats)).Suffix.Should().EndWith("3 processes running the game's executable, added together");
        InEnglish(() => GameMemoryText.VramTooltip(row)).Should().Contain(Environment.NewLine + "3 processes");
        InEnglish(() => GameMemoryText.VramCard(Measured())).Suffix.Should().NotContain("processes", "one process is the normal case");
    }

    [Fact]
    public void TheGridCellsAreMedianAndPeakAndTheTooltipsCarryTheRest()
    {
        InEnglish(() => GameMemoryText.VramCell(Measured())).Should().Be("6.50 GB · 7.00 GB");
        InEnglish(() => GameMemoryText.RamCell(Measured(), _noStats)).Should().Be("3.00 GB · 3.50 GB");
        InEnglish(() => GameMemoryText.VramTooltip(Measured())).Should().Be("Dedicated GPU memory, as Task Manager counts it — mean 6.25 GB · median 6.50 GB · peak 7.00 GB. Shared GPU memory, peak 512 MB.");
        InEnglish(() => GameMemoryText.RamTooltip(Measured(), _noStats)).Should().Be("Private working set (Task Manager's \"Memory\") — mean 2.93 GB · median 3.00 GB · peak 3.50 GB. Working set, peak 4.00 GB · commit, peak 5.00 GB.");
    }

    [Fact]
    public void TheLiveLineAndTheExportLine()
    {
        InEnglish(() => GameMemoryText.Live(6656, 3072)).Should().Be("This game: VRAM 6.50 GB · RAM 3.00 GB");
        InEnglish(() => GameMemoryText.Live(null, 3072)).Should().Be("This game: VRAM N/A · RAM 3.00 GB", "a game that has made no GPU allocation yet has no counter instance");

        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
        try
        {
            GameMemoryText.ExportLine(Measured() with { GameVramDedicatedAvgMb = 6400.24 }).Should().Be(
                "vram_dedicated_mb avg=6400.2 median=6656 max=7168; vram_shared_mb max=512; ram_private_ws_mb avg=3000 median=3072 max=3584; ram_ws_mb max=4096; commit_mb max=5120; processes=1; source=counters,ex2,held",
                "MiB, invariant: never the vi-VN decimal comma");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>The statistics table: catalogue order, each series in its own unit and under its chart label, a series with no reading left out.</summary>
    [Fact]
    public void TheStatisticsTableIsTheStoredValuesInEachSeriesOwnUnit()
    {
        var stats = new Dictionary<string, SensorSeriesStats>(StringComparer.Ordinal)
        {
            [SensorSeriesCatalog.GameRamPrivate] = new SensorSeriesStats(600, 3000, 3072, 512, 3584),
            [SensorSeriesCatalog.GpuTemp] = new SensorSeriesStats(600, 64.6, 65, 41, 72),
            [SensorSeriesCatalog.GpuPower] = new SensorSeriesStats(600, 250.4, 260, 30, 330),
            [SensorSeriesCatalog.GpuLoad] = new SensorSeriesStats(600, 91.2, 97, 3, 99),
            [SensorSeriesCatalog.CpuTemp] = new SensorSeriesStats(0, null, null, null, null),
        };

        IReadOnlyList<SensorStatRowModel> rows = InEnglish(() => SensorStatsTable.Rows(stats));

        rows.Select(static r => r.Series).Should().Equal(Strings.Sensors_Series_GpuTemp, Strings.Sensors_Series_GpuLoad, Strings.Sensors_Series_GpuPower, Strings.Sensors_Series_GameRam);
        rows[0].Should().Be(new SensorStatRowModel(Strings.Sensors_Series_GpuTemp, "65 °C", "65 °C", "41 °C", "72 °C", "600"));
        rows[1].Mean.Should().Be("91%");
        rows[2].Max.Should().Be("330 W");
        rows[3].Should().Be(new SensorStatRowModel(Strings.Sensors_Series_GameRam, "2.93 GB", "3.00 GB", "512 MB", "3.50 GB", "600"));
        SensorStatsTable.Rows(_noStats).Should().BeEmpty("a session recorded before beta.12 stored none");
    }
}

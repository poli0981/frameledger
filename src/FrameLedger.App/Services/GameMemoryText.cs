// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;

namespace FrameLedger.App.Services;

/// <summary>
/// The game process's own memory as the pages state it (beta.12, D43; <c>03_METRICS</c> §Game process memory): the summary's
/// two cards, the Sessions grid's two cells with their tooltips, the live card's line and the CSV header's line — each from
/// the STORED columns and <c>sensor_stats_json</c>, never recomputed (<c>08_UI</c>). Both tiers: the memory is read from
/// outside the game, so a session that was not hooked has it too.
/// </summary>
/// <remarks>
/// <b>The private working set, or the working set labelled.</b> Task Manager's "Memory" is the private working set, which
/// Windows reports only from 10 22H2 / 11 22H2 with the September 2023 update on. Where it is N/A the working set is shown in
/// its place and SAYS so — a different quantity (shared pages included), never passed off as the same one.
/// </remarks>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public static class GameMemoryText
{
    /// <summary>The summary's video-memory card: the dedicated median, its peak and the shared peak beneath.</summary>
    public static StatCardModel VramCard(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.GameVramDedicatedMedianMb is not double median)
        {
            return new StatCardModel(Strings.Summary_Stat_GameVram, Strings.Common_NotAvailable);
        }

        string suffix = string.Format(CultureInfo.CurrentCulture, Strings.Summary_Stat_GameVram_Suffix_Format,
            Formats.Memory(row.GameVramDedicatedMaxMb), Formats.Memory(row.GameVramSharedMaxMb));
        return new StatCardModel(Strings.Summary_Stat_GameVram, Formats.Memory(median), WithProcesses(suffix, row));
    }

    /// <summary>The summary's memory card: the private working set's median and peak with the commit peak, or the working set labelled.</summary>
    public static StatCardModel RamCard(SessionRow row, IReadOnlyDictionary<string, SensorSeriesStats> stats)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(stats);
        if (row.GameRamPrivateMedianMb is double median)
        {
            string suffix = string.Format(CultureInfo.CurrentCulture, Strings.Summary_Stat_GameRam_Suffix_Format,
                Formats.Memory(row.GameRamPrivateMaxMb), Formats.Memory(row.GameCommitMaxMb));
            return new StatCardModel(Strings.Summary_Stat_GameRam, Formats.Memory(median), WithProcesses(suffix, row));
        }

        if (WorkingSetMedian(stats) is double ws)
        {
            string suffix = string.Format(CultureInfo.CurrentCulture, Strings.Summary_Stat_GameRamWs_Suffix_Format, Formats.Memory(row.GameRamWorkingSetMaxMb));
            return new StatCardModel(Strings.Summary_Stat_GameRamWs, Formats.Memory(ws), WithProcesses(suffix, row));
        }

        return new StatCardModel(Strings.Summary_Stat_GameRam, Strings.Common_NotAvailable);
    }

    /// <summary>The Sessions grid's video-memory cell: median · peak, or N/A.</summary>
    public static string VramCell(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return Pair(row.GameVramDedicatedMedianMb, row.GameVramDedicatedMaxMb);
    }

    /// <summary>The grid's video-memory tooltip: mean, median, peak and the shared peak; null when nothing was read.</summary>
    public static string? VramTooltip(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.GameVramDedicatedMaxMb is null)
        {
            return null;
        }

        string text = string.Format(CultureInfo.CurrentCulture, Strings.Sessions_GameVram_Tooltip_Format, Formats.Memory(row.GameVramDedicatedAvgMb),
            Formats.Memory(row.GameVramDedicatedMedianMb), Formats.Memory(row.GameVramDedicatedMaxMb), Formats.Memory(row.GameVramSharedMaxMb));
        return WithProcesses(text, row, Environment.NewLine);
    }

    /// <summary>The grid's memory cell: the private working set's median · peak, the working set's labelled, or N/A.</summary>
    public static string RamCell(SessionRow row, IReadOnlyDictionary<string, SensorSeriesStats> stats)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(stats);
        if (row.GameRamPrivateMaxMb is not null)
        {
            return Pair(row.GameRamPrivateMedianMb, row.GameRamPrivateMaxMb);
        }

        return row.GameRamWorkingSetMaxMb is not null
            ? string.Format(CultureInfo.CurrentCulture, Strings.Sessions_GameRamWs_Cell_Format, Formats.Memory(WorkingSetMedian(stats)), Formats.Memory(row.GameRamWorkingSetMaxMb))
            : Strings.Common_NotAvailable;
    }

    /// <summary>The grid's memory tooltip; null when nothing was read.</summary>
    public static string? RamTooltip(SessionRow row, IReadOnlyDictionary<string, SensorSeriesStats> stats)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(stats);
        string? text = row.GameRamPrivateMaxMb is not null
            ? string.Format(CultureInfo.CurrentCulture, Strings.Sessions_GameRam_Tooltip_Format, Formats.Memory(row.GameRamPrivateAvgMb), Formats.Memory(row.GameRamPrivateMedianMb),
                Formats.Memory(row.GameRamPrivateMaxMb), Formats.Memory(row.GameRamWorkingSetMaxMb), Formats.Memory(row.GameCommitMaxMb))
            : row.GameRamWorkingSetMaxMb is not null
                ? string.Format(CultureInfo.CurrentCulture, Strings.Sessions_GameRamWs_Tooltip_Format, Formats.Memory(WorkingSetMedian(stats)), Formats.Memory(row.GameRamWorkingSetMaxMb))
                : null;
        return text is null ? null : WithProcesses(text, row, Environment.NewLine);
    }

    /// <summary>The live card's line from the newest tick (<c>SessionProgress</c> / <c>SessionHeld</c>); empty when neither half was read.</summary>
    public static string Live(double? vramMb, double? ramMb) => vramMb is null && ramMb is null
        ? string.Empty
        : string.Format(CultureInfo.CurrentCulture, Strings.Dashboard_Live_GameMemory_Format, Formats.Memory(vramMb), Formats.Memory(ramMb));

    /// <summary>
    /// The CSV header's <c># game_memory:</c> line (<c>03_METRICS</c> §Export schema): every stored column in MiB, invariant,
    /// or <c>N/A</c> for a session recorded before beta.12 or one where nothing answered. A session's figure, not a frame's,
    /// so a header line — like <c># display:</c> — and never the per-frame <c>vram_mb</c> column.
    /// </summary>
    public static string ExportLine(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.GameVramDedicatedMaxMb is null && row.GameRamPrivateMaxMb is null && row.GameRamWorkingSetMaxMb is null && row.GameCommitMaxMb is null)
        {
            return "N/A";
        }

        return string.Join("; ",
            "vram_dedicated_mb avg=" + Mib(row.GameVramDedicatedAvgMb) + " median=" + Mib(row.GameVramDedicatedMedianMb) + " max=" + Mib(row.GameVramDedicatedMaxMb),
            "vram_shared_mb max=" + Mib(row.GameVramSharedMaxMb),
            "ram_private_ws_mb avg=" + Mib(row.GameRamPrivateAvgMb) + " median=" + Mib(row.GameRamPrivateMedianMb) + " max=" + Mib(row.GameRamPrivateMaxMb),
            "ram_ws_mb max=" + Mib(row.GameRamWorkingSetMaxMb),
            "commit_mb max=" + Mib(row.GameCommitMaxMb),
            "processes=" + (row.GameMemoryProcesses?.ToString(CultureInfo.InvariantCulture) ?? "N/A"),
            "source=" + (row.GameMemorySource ?? "N/A"));
    }

    /// <summary>The working set's stored median — the one statistic of it with no column of its own.</summary>
    private static double? WorkingSetMedian(IReadOnlyDictionary<string, SensorSeriesStats> stats) =>
        stats.TryGetValue(SensorSeriesCatalog.GameRamWorkingSet, out SensorSeriesStats? ws) ? ws.Median : null;

    private static string Pair(double? median, double? peak) => median is null && peak is null
        ? Strings.Common_NotAvailable
        : Formats.Memory(median) + " · " + Formats.Memory(peak);

    /// <summary>An unpinned hold sums every process running the executable; more than one is said wherever the figure is.</summary>
    private static string WithProcesses(string text, SessionRow row, string separator = " · ") => row.GameMemoryProcesses is > 1 and int n
        ? text + separator + string.Format(CultureInfo.CurrentCulture, Strings.GameMemory_Processes_Format, n)
        : text;

    private static string Mib(double? value) => value is double v ? v.ToString("0.#", CultureInfo.InvariantCulture) : "N/A";
}

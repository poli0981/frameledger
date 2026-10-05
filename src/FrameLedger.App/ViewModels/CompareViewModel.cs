// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.App.Charts;
using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;
using ScottPlot;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// Compare (<c>08_UI</c> §Compare): pick 2–5 sessions across games; the overlaid percentile curves and the stat
/// table with the best value of each row highlighted; FG columns are Native and Displayed, always apart. FR-6.2:
/// a selection that mixes tiers is a blocking question first, and a comparison that proceeds carries the tier
/// legend on the chart with the Tier-2 cells at N/A — a comparison with nothing on one side is shown as one.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed partial class CompareViewModel : ObservableObject
{
    public const int MinSessions = 2;

    public const int MaxSessions = 5;

    private readonly GameLibrary _library;
    private readonly SessionSeriesLoader _loader;
    private readonly IMixedTierPrompt _mixed;
    private readonly IFileSaver _saver;
    private readonly IMessageStrip _strip;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private bool _canCompare;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private bool _isMixed;

    [ObservableProperty]
    private string? _tierLegend;

    public CompareViewModel(GameLibrary library, SessionSeriesLoader loader, IMixedTierPrompt mixed, IFileSaver saver, IMessageStrip strip)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _mixed = mixed ?? throw new ArgumentNullException(nameof(mixed));
        _saver = saver ?? throw new ArgumentNullException(nameof(saver));
        _strip = strip ?? throw new ArgumentNullException(nameof(strip));
        Pending = LoadAsync();
    }

    public static string Header => Strings.Compare_Header;

    public static string EmptyText => Strings.Compare_NoCandidates;

    public static string PickHeader => Strings.Compare_Pick_Header;

    public static string RunText => Strings.Compare_Run;

    public static string ExportPngText => Strings.Compare_Export_Png;

    public static string CurvesHeader => Strings.Compare_Curves_Header;

    public static string TableHeader => Strings.Compare_Table_Header;

    public static string MetricHeader => Strings.Compare_Table_Metric;

    public ObservableCollection<CompareCandidateViewModel> Candidates { get; } = [];

    public ObservableCollection<CompareCandidateViewModel> Compared { get; } = [];

    public ObservableCollection<CompareRowViewModel> Rows { get; } = [];

    public IReadOnlyList<CompareCurve> Curves { get; private set; } = [];

    public string SelectedText => string.Format(CultureInfo.CurrentCulture, Strings.Compare_Selected_Format, SelectedCount);

    /// <summary>The load or the comparison in flight, for a test to await.</summary>
    public Task Pending { get; private set; }

    /// <summary>Raised when <see cref="Curves"/> and <see cref="Rows"/> were rebuilt, so the page redraws.</summary>
    public event EventHandler? Compared2;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IReadOnlyList<RecentSession> recent = await _library.RecentAsync(200, ct).ConfigureAwait(true);
        foreach (CompareCandidateViewModel old in Candidates)
        {
            old.PropertyChanged -= OnCandidateChanged;
        }

        Candidates.Clear();
        foreach (RecentSession r in recent)
        {
            var candidate = new CompareCandidateViewModel(r);
            candidate.PropertyChanged += OnCandidateChanged;
            Candidates.Add(candidate);
        }

        IsEmpty = Candidates.Count == 0;
        Recount();
    }

    [RelayCommand]
    private Task CompareAsync() => Run(CompareCoreAsync);

    public Task ExportPngAsync(Plot plot)
    {
        ArgumentNullException.ThrowIfNull(plot);
        return Run(async () =>
        {
            string? path = _saver.PickSavePath("PNG|*.png", "compare-" + DateTimeOffset.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".png");
            if (path is null)
            {
                return;
            }

            await Task.Run(() => Charts.ChartTheme.SavePng(plot, path, 1600, 800)).ConfigureAwait(true);
            _strip.Success(Strings.Compare_Header, string.Format(CultureInfo.CurrentCulture, Strings.Summary_Exported_Format, path));
        });
    }

    private async Task Run(Func<Task> work)
    {
        Task running = work();
        Pending = running;
        await running.ConfigureAwait(true);
    }

    private async Task CompareCoreAsync()
    {
        CompareCandidateViewModel[] picked = [.. Candidates.Where(static c => c.IsSelected)];
        if (picked.Length < MinSessions || picked.Length > MaxSessions)
        {
            return;
        }

        bool mixed = picked.Any(static c => c.IsHooked) && picked.Any(static c => !c.IsHooked);
        if (mixed && !await _mixed.AcknowledgeAsync().ConfigureAwait(true))
        {
            return;
        }

        var curves = new List<CompareCurve>();
        foreach (CompareCandidateViewModel c in picked.Where(static c => c.IsHooked))
        {
            SessionSeries? series = await _loader.LoadAsync(c.Id).ConfigureAwait(true);
            if (series is not null)
            {
                (double[] xs, double[] ys) = PercentileCurve.Of(series.AppFrameTimesMs);
                curves.Add(new CompareCurve(c.Label, xs, ys));
            }
        }

        Curves = curves;
        IsMixed = mixed;
        TierLegend = mixed ? Strings.Compare_Mixed_Legend : null;
        Compared.Clear();
        foreach (CompareCandidateViewModel c in picked)
        {
            Compared.Add(c);
        }

        BuildRows(picked);
        HasResult = true;
        Compared2?.Invoke(this, EventArgs.Empty);
    }

    private void BuildRows(CompareCandidateViewModel[] picked)
    {
        Rows.Clear();
        SessionRow[] rows = [.. picked.Select(static c => c.Row)];
        AddFrameRows(rows);
        AddMachineRows(rows);
        Rows.Add(new CompareRowViewModel(Strings.Compare_Metric_Duration, [.. rows.Select(static r => new CompareCell(Formats.Duration(r.DurationSeconds), false))]));

        // beta.10: how each session was shown, both tiers. Facts, not scores: no mode is better than another, so none is best.
        Rows.Add(new CompareRowViewModel(Strings.Compare_Metric_Display, [.. rows.Select(static r => new CompareCell(DisplayText.Shares(DisplayText.Of(r)), false))]));
        Rows.Add(new CompareRowViewModel(Strings.Compare_Metric_Window, [.. rows.Select(static r => new CompareCell(DisplayText.Window(DisplayText.Of(r)), false))]));
        Rows.Add(new CompareRowViewModel(Strings.Compare_Metric_Monitor, [.. rows.Select(static r => new CompareCell(DisplayText.Monitor(DisplayText.Of(r)), false))]));
    }

    /// <summary>
    /// The frame rate's rows, hooked sessions only. A statistic a present-only session has over its presents is labelled
    /// <c>(presented)</c> in its cell (beta.14, rule 6) — beside a session whose same statistic is over the game's own frames,
    /// an unlabelled number would compare two different things as one.
    /// </summary>
    private void AddFrameRows(SessionRow[] rows)
    {
        Rows.Add(Row(Strings.Compare_Metric_Native, rows, static r => FpsPresentation.FromRow(r) is { Kind: FpsReadoutKind.Generated or FpsReadoutKind.None } m ? m.Native : null, Formats.Fps, higherIsBetter: true));
        Rows.Add(Row(Strings.Compare_Metric_Displayed, rows, static r => FpsPresentation.FromRow(r) is { Kind: FpsReadoutKind.Generated } m ? m.Displayed : null, Formats.Fps, higherIsBetter: true));
        Rows.Add(Row(Strings.Compare_Metric_Presented, rows, static r => FpsPresentation.FromRow(r) is { Kind: FpsReadoutKind.Presented or FpsReadoutKind.IdentifiedUncounted } m ? m.Presented : null, Formats.Fps, higherIsBetter: true));
        Rows.Add(Row(Strings.Compare_Metric_Median, rows, static r => Hooked(r, r.MedianFps), Formats.Fps, higherIsBetter: true, PresentedLabel));
        Rows.Add(Row(Strings.Compare_Metric_P1Low, rows, static r => Hooked(r, r.P1LowFps), Formats.Fps, higherIsBetter: true, PresentedLabel));
        Rows.Add(Row(Strings.Compare_Metric_P01Low, rows, static r => Hooked(r, r.P01LowFps), Formats.Fps, higherIsBetter: true, PresentedLabel));
        Rows.Add(Row(Strings.Compare_Metric_StutterPct, rows, static r => Hooked(r, r.StutterTimePct), Formats.Share, higherIsBetter: false, PresentedLabel));

        // beta.14 (D49): pacing — less time below 60 and less change from frame to frame is better; the refresh share is the
        // display's, over presents, generated frames included, and is named for it rather than labelled presented.
        Rows.Add(Row(Strings.Compare_Metric_TimeBelow60, rows, static r => Hooked(r, r.TimeBelow60Pct), Formats.Share, higherIsBetter: false, PresentedLabel));
        Rows.Add(Row(Strings.Compare_Metric_TimeBelowRefresh, rows, static r => Hooked(r, r.TimeBelowRefreshPct), Formats.Share, higherIsBetter: false));
        Rows.Add(Row(Strings.Compare_Metric_FrameToFrame, rows, static r => Hooked(r, r.FrametimeDeltaMeanMs), Formats.Milliseconds, higherIsBetter: false, PresentedLabel));

        // What the presents asked for, and the render scale: settings, not scores — no best.
        Rows.Add(Row(Strings.Compare_Metric_Vsync, rows, static r => Hooked(r, r.VsyncPresentPct), Formats.Share, higherIsBetter: null));
        Rows.Add(Row(Strings.Compare_Metric_RenderScale, rows, static r => r.UpscaleRatio is > 0 ? 100.0 / r.UpscaleRatio.Value : null, Formats.Share, higherIsBetter: null));
    }

    /// <summary>The machine's rows, both tiers.</summary>
    private void AddMachineRows(SessionRow[] rows)
    {
        Rows.Add(Row(Strings.Compare_Metric_MaxGpuTemp, rows, static r => r.MaxGpuTemp, Formats.Temperature, higherIsBetter: false));
        // A load is neither better high nor low (beta.8): a GPU at 99 % may be the bottleneck or simply busy, so neither row
        // names a best.
        Rows.Add(Row(Strings.Compare_Metric_AvgGpuLoad, rows, static r => r.AvgGpuLoad, Formats.Percent, higherIsBetter: null));
        Rows.Add(Row(Strings.Compare_Metric_AvgCpuLoad, rows, static r => r.AvgCpuLoad, Formats.Percent, higherIsBetter: null));
        Rows.Add(Row(Strings.Compare_Metric_MaxCpuTemp, rows, static r => r.MaxCpuTemp, Formats.Temperature, higherIsBetter: false));

        // beta.12 (D43), both tiers: the game's own memory. Facts, not scores — a game that uses more of a card it has is not
        // worse — so no row names a best. The private working set only, as on the Trend.
        Rows.Add(Row(Strings.Compare_Metric_GameVramMedian, rows, static r => r.GameVramDedicatedMedianMb, Formats.Memory, higherIsBetter: null));
        Rows.Add(Row(Strings.Compare_Metric_GameVramPeak, rows, static r => r.GameVramDedicatedMaxMb, Formats.Memory, higherIsBetter: null));
        Rows.Add(Row(Strings.Compare_Metric_GameRamMedian, rows, static r => r.GameRamPrivateMedianMb, Formats.Memory, higherIsBetter: null));
        Rows.Add(Row(Strings.Compare_Metric_GameRamPeak, rows, static r => r.GameRamPrivateMaxMb, Formats.Memory, higherIsBetter: null));

        // beta.14 (D49): the card. A clock and a power limit are facts about the card, not scores; a cooler memory and more
        // of the game's own frames per watt are better.
        Rows.Add(Row(Strings.Compare_Metric_GpuCoreClock, rows, static r => r.AvgGpuCoreClockMhz, Formats.Frequency, higherIsBetter: null));
        Rows.Add(Row(Strings.Compare_Metric_MaxGpuMemTemp, rows, static r => r.MaxGpuMemTemp, Formats.Temperature, higherIsBetter: false));
        Rows.Add(Row(Strings.Compare_Metric_PowerLimit, rows, static r => r.PowerLimitPct, Formats.Share, higherIsBetter: null));
        Rows.Add(Row(Strings.Compare_Metric_Efficiency, rows, static r => r.AppFramesPerJoule, Formats.FramesPerJoule, higherIsBetter: true));
    }

    private static double? Hooked(SessionRow row, double? value) => row.Tier == Domain.Sessions.CaptureTier.Hooked ? value : null;

    /// <summary>The <c>(presented)</c> label for a hooked session whose statistics are over presents; null otherwise.</summary>
    private static string? PresentedLabel(SessionRow row) =>
        row.Tier == Domain.Sessions.CaptureTier.Hooked && FpsPresentation.FromRow(row).Kind == FpsReadoutKind.Presented ? Strings.Summary_Lows_Presented : null;

    /// <summary>
    /// One row: the value per session, the best (never an N/A) flagged; a row where nothing has a value is all N/A and nothing
    /// is best, and a row that ranks nothing (<paramref name="higherIsBetter"/> null) flags none. Since beta.8 the best is the
    /// best as SHOWN: two sessions that both read "62" are both best — 62.4 against 62.3 is not a difference the reader sees.
    /// </summary>
    internal static CompareRowViewModel Row(string metric, IReadOnlyList<SessionRow> rows, Func<SessionRow, double?> value, Func<double?, string> text, bool? higherIsBetter,
        Func<SessionRow, string?>? label = null)
    {
        double?[] values = [.. rows.Select(value)];
        double? best = higherIsBetter switch
        {
            true => values.Where(static v => v.HasValue).Max(),
            false => values.Where(static v => v.HasValue).Min(),
            null => null,
        };
        string? bestText = best.HasValue ? text(best) : null;

        // The best is decided on the number as shown; a label beside it (beta.14) says what the number is over, not how good.
        return new CompareRowViewModel(metric, [.. values.Select((v, i) => new CompareCell(
            Labelled(text(v), v.HasValue ? label?.Invoke(rows[i]) : null),
            bestText is not null && v.HasValue && string.Equals(text(v), bestText, StringComparison.Ordinal)))]);
    }

    private static string Labelled(string text, string? label) => label is null ? text : text + " " + label;

    private void OnCandidateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(CompareCandidateViewModel.IsSelected), StringComparison.Ordinal))
        {
            Recount();
        }
    }

    private void Recount()
    {
        SelectedCount = Candidates.Count(static c => c.IsSelected);
        CanCompare = SelectedCount is >= MinSessions and <= MaxSessions;
        OnPropertyChanged(nameof(SelectedText));
    }
}

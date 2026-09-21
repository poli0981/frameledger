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

            await Task.Run(() => plot.SavePng(path, 1600, 800)).ConfigureAwait(true);
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
        Rows.Add(Row(Strings.Compare_Metric_Native, rows, static r => FpsPresentation.FromRow(r) is { Kind: FpsReadoutKind.Generated or FpsReadoutKind.None } m ? m.Native : null, Formats.Fps, higherIsBetter: true));
        Rows.Add(Row(Strings.Compare_Metric_Displayed, rows, static r => FpsPresentation.FromRow(r) is { Kind: FpsReadoutKind.Generated } m ? m.Displayed : null, Formats.Fps, higherIsBetter: true));
        Rows.Add(Row(Strings.Compare_Metric_Presented, rows, static r => FpsPresentation.FromRow(r) is { Kind: FpsReadoutKind.Presented or FpsReadoutKind.IdentifiedUncounted } m ? m.Presented : null, Formats.Fps, higherIsBetter: true));
        Rows.Add(Row(Strings.Compare_Metric_Median, rows, static r => r.Tier == Domain.Sessions.CaptureTier.Hooked ? r.MedianFps : null, Formats.Fps, higherIsBetter: true));
        Rows.Add(Row(Strings.Compare_Metric_P1Low, rows, static r => r.Tier == Domain.Sessions.CaptureTier.Hooked ? r.P1LowFps : null, Formats.Fps, higherIsBetter: true));
        Rows.Add(Row(Strings.Compare_Metric_P01Low, rows, static r => r.Tier == Domain.Sessions.CaptureTier.Hooked ? r.P01LowFps : null, Formats.Fps, higherIsBetter: true));
        Rows.Add(Row(Strings.Compare_Metric_StutterPct, rows, static r => r.Tier == Domain.Sessions.CaptureTier.Hooked ? r.StutterTimePct : null, static v => v is double d ? d.ToString("0.0", CultureInfo.CurrentCulture) + "%" : Strings.Common_NotAvailable, higherIsBetter: false));
        Rows.Add(Row(Strings.Compare_Metric_MaxGpuTemp, rows, static r => r.MaxGpuTemp, Formats.Temperature, higherIsBetter: false));
        Rows.Add(Row(Strings.Compare_Metric_AvgGpuLoad, rows, static r => r.AvgGpuLoad, Formats.Percent, higherIsBetter: true));
        Rows.Add(Row(Strings.Compare_Metric_AvgCpuLoad, rows, static r => r.AvgCpuLoad, Formats.Percent, higherIsBetter: false));
        Rows.Add(Row(Strings.Compare_Metric_MaxCpuTemp, rows, static r => r.MaxCpuTemp, Formats.Temperature, higherIsBetter: false));
        Rows.Add(new CompareRowViewModel(Strings.Compare_Metric_Duration, [.. rows.Select(static r => new CompareCell(Formats.Duration(r.DurationSeconds), false))]));
    }

    /// <summary>One row: the value per session, the best (never an N/A) flagged; a row where nothing has a value is all N/A and nothing is best.</summary>
    internal static CompareRowViewModel Row(string metric, IReadOnlyList<SessionRow> rows, Func<SessionRow, double?> value, Func<double?, string> text, bool higherIsBetter)
    {
        double?[] values = [.. rows.Select(value)];
        double? best = higherIsBetter ? values.Where(static v => v.HasValue).Max() : values.Where(static v => v.HasValue).Min();
        return new CompareRowViewModel(metric, [.. values.Select(v => new CompareCell(text(v), best.HasValue && v.HasValue && v.Value == best.Value))]);
    }

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

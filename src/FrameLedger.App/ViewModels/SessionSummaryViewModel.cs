using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.App.Charts;
using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.TriState;
using FrameLedger.Domain.Sessions;
using ScottPlot;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// The session summary (<c>08_UI</c> §Session summary): stat cards from the stored aggregates (never recomputed),
/// the chips with FR-8.3's override, the crash and safety-unhook bars, the decoded series for the charts, the
/// tags/notes editor, and FR-9's three exports.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed partial class SessionSummaryViewModel : ObservableObject
{
    private readonly ISessionRepository _sessions;
    private readonly IGameRepository _games;
    private readonly ISessionAnnotationRepository _annotations;
    private readonly IHardwareSnapshotRepository _hardware;
    private readonly SessionSeriesLoader _loader;
    private readonly ITriStateOverridePrompt _override;
    private readonly IFileSaver _saver;
    private readonly IMessageStrip _strip;
    private SessionAnnotation? _annotation;
    private IReadOnlyList<SegmentRow> _segments = [];
    private HardwareSnapshot? _snapshot;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _line = string.Empty;

    [ObservableProperty]
    private FpsReadoutModel _readout = FpsReadoutModel.Unavailable;

    [ObservableProperty]
    private bool _isHooked;

    [ObservableProperty]
    private bool _isCrashed;

    [ObservableProperty]
    private bool _isUnhooked;

    [ObservableProperty]
    private bool _notFound;

    [ObservableProperty]
    private bool _hasDisplayed;

    [ObservableProperty]
    private bool _showDisplayed;

    [ObservableProperty]
    private bool _hasSensors;

    [ObservableProperty]
    private bool _showSensors;

    [ObservableProperty]
    private string _tags = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _decimationNote = string.Empty;

    public SessionSummaryViewModel(ISessionRepository sessions, IGameRepository games, ISessionAnnotationRepository annotations,
        IHardwareSnapshotRepository hardware, SessionSeriesLoader loader, ITriStateOverridePrompt overridePrompt, IFileSaver saver, IMessageStrip strip)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _annotations = annotations ?? throw new ArgumentNullException(nameof(annotations));
        _hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _override = overridePrompt ?? throw new ArgumentNullException(nameof(overridePrompt));
        _saver = saver ?? throw new ArgumentNullException(nameof(saver));
        _strip = strip ?? throw new ArgumentNullException(nameof(strip));
    }

    public static string Tier2Text => Strings.Summary_Tier2_Body;

    public static string CrashTitle => Strings.Summary_Crash_Title;

    public static string CrashBody => Strings.Summary_Crash_Body;

    public static string UnhookedTitle => Strings.Summary_Unhooked_Title;

    public static string UnhookedBody => Strings.Summary_Unhooked_Body;

    public static string NotFoundText => Strings.Summary_NotFound;

    public static string FrametimeHeader => Strings.Summary_Frametime_Header;

    public static string DistributionHeader => Strings.Summary_Distribution_Header;

    public static string ShowDisplayedText => Strings.Summary_Show_Displayed;

    public static string ShowSensorsText => Strings.Summary_Show_Sensors;

    public static string AnnotationsHeader => Strings.Summary_Annotations_Header;

    public static string TagsPlaceholder => Strings.Summary_Tags_Placeholder;

    public static string NotesPlaceholder => Strings.Summary_Notes_Placeholder;

    public static string SaveText => Strings.Summary_Save;

    public static string ExportCsvText => Strings.Summary_Export_Csv;

    public static string ExportJsonText => Strings.Summary_Export_Json;

    public static string ExportPngText => Strings.Summary_Export_Png;

    public SessionRow? Row { get; private set; }

    public GameRow? Game { get; private set; }

    public SessionSeries? Series { get; private set; }

    public ObservableCollection<StatCardModel> Stats { get; } = [];

    public ObservableCollection<TriStateChipModel> Chips { get; } = [];

    /// <summary>The action in flight, for a test to await.</summary>
    public Task Pending { get; private set; } = Task.CompletedTask;

    /// <summary>Raised after a load or an override so the window redraws the charts and chips.</summary>
    public event EventHandler? Presented;

    public async Task LoadAsync(long sessionId, CancellationToken ct = default)
    {
        Row = await _sessions.FindByIdAsync(sessionId, ct).ConfigureAwait(true);
        Game = Row is null ? null : await _games.FindByIdAsync(Row.GameId, ct).ConfigureAwait(true);
        if (Row is null || Game is null)
        {
            NotFound = true;
            return;
        }

        NotFound = false;
        _annotation = await _annotations.FindAsync(sessionId, ct).ConfigureAwait(true);
        _segments = await _sessions.FindSegmentsAsync(sessionId, ct).ConfigureAwait(true);
        _snapshot = await _hardware.FindAsync(Row.SnapshotId, ct).ConfigureAwait(true);
        Series = Row.Tier == CaptureTier.Hooked ? await _loader.LoadAsync(sessionId, ct).ConfigureAwait(true) : null;
        Present();
    }

    [RelayCommand]
    private Task OverrideAsync(TriStateChipModel? chip) => Run(() => OverrideCoreAsync(chip));

    [RelayCommand]
    private Task SaveAnnotationAsync() => Run(SaveAnnotationCoreAsync);

    [RelayCommand]
    private Task ExportCsvAsync() => Run(() => ExportAsync("csv", "CSV|*.csv", WriteCsv));

    [RelayCommand]
    private Task ExportJsonAsync() => Run(() => ExportAsync("json", "JSON|*.json", WriteJson));

    /// <summary>FR-9.3: the window hands in the plot it shows; the picker and the strip are the same as the other two.</summary>
    public Task ExportPngAsync(Plot plot)
    {
        ArgumentNullException.ThrowIfNull(plot);
        return Run(() => ExportAsync("png", "PNG|*.png", path => plot.SavePng(path, 1600, 700)));
    }

    private async Task Run(Func<Task> work)
    {
        Task running = work();
        Pending = running;
        await running.ConfigureAwait(true);
    }

    private async Task OverrideCoreAsync(TriStateChipModel? chip)
    {
        if (chip is null || Row is null || Game is null)
        {
            return;
        }

        TriStateOverrideChoice? choice = await _override.AskAsync(chip).ConfigureAwait(true);
        if (choice is null)
        {
            return;
        }

        TriStateKind kind = chip.Kind;
        SessionAnnotation current = _annotation ?? new SessionAnnotation { SessionId = Row.Id };
        SessionAnnotation next = kind switch
        {
            TriStateKind.RayTracing => current with { RtOverride = choice.Override },
            TriStateKind.PathTracing => current with { PtOverride = choice.Override },
            _ => current with { RrOverride = choice.Override },
        };
        await _annotations.UpsertAsync(next, CancellationToken.None).ConfigureAwait(true);
        if (choice.SetAsGameDefault)
        {
            await _games.SetTriStateDefaultAsync(Game.Id, kind, choice.Override ?? Domain.Metrics.Tri.NotApplicable, CancellationToken.None).ConfigureAwait(true);
            Game = await _games.FindByIdAsync(Game.Id, CancellationToken.None).ConfigureAwait(true) ?? Game;
        }

        _annotation = await _annotations.FindAsync(Row.Id, CancellationToken.None).ConfigureAwait(true);
        PresentChips();
        Presented?.Invoke(this, EventArgs.Empty);
    }

    private async Task SaveAnnotationCoreAsync()
    {
        if (Row is null)
        {
            return;
        }

        string[] tags = [.. Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.CurrentCultureIgnoreCase)];
        SessionAnnotation current = _annotation ?? new SessionAnnotation { SessionId = Row.Id };
        await _annotations.UpsertAsync(current with { Tags = tags, Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim() }, CancellationToken.None).ConfigureAwait(true);
        _annotation = await _annotations.FindAsync(Row.Id, CancellationToken.None).ConfigureAwait(true);
        _strip.Success(Strings.Summary_Annotations_Header, Strings.Summary_Saved);
    }

    private async Task ExportAsync(string extension, string filter, Action<string> write)
    {
        if (Row is null || Game is null)
        {
            return;
        }

        string suggested = Sanitise(Game.Name) + "-" + Row.StartedAt.ToLocalTime().ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + "." + extension;
        string? path = _saver.PickSavePath(filter, suggested);
        if (path is null)
        {
            return;
        }

        try
        {
            await Task.Run(() => write(path)).ConfigureAwait(true);
            _strip.Success(Strings.Games_Header, string.Format(CultureInfo.CurrentCulture, Strings.Summary_Exported_Format, path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _strip.Warn(Strings.Games_Header, string.Format(CultureInfo.CurrentCulture, Strings.Summary_Export_Failed_Format, ex.Message));
        }
    }

    private void WriteCsv(string path)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        SessionExporter.WriteCsv(writer, Row!, Game!, _snapshot, _segments, Series);
    }

    private void WriteJson(string path)
    {
        using FileStream stream = File.Create(path);
        SessionExporter.WriteJson(stream, SessionExporter.Document(Row!, Game!, _snapshot, _segments, _annotation));
    }

    private void Present()
    {
        SessionRow row = Row!;
        Title = string.Format(CultureInfo.CurrentCulture, Strings.Summary_Title_Format, Game!.Name, Formats.Date(row.StartedAt));
        IsHooked = row.Tier == CaptureTier.Hooked;
        IsCrashed = row.ExitStatus == ExitStatus.Crashed;
        IsUnhooked = row.ExitStatus == ExitStatus.UnhookedSafety;
        Readout = FpsPresentation.FromRow(row);
        Line = string.Format(CultureInfo.CurrentCulture, Strings.Summary_Line_Format,
            IsHooked ? Formats.Api(row.Api) : Strings.Tier_NotHooked, row.PresentMode ?? Strings.Common_NotAvailable,
            IsHooked ? Formats.Upscaler(row.Upscaler) : Strings.Common_NotAvailable, Formats.Resolution(row.RenderW, row.RenderH, row.OutputW, row.OutputH));
        Tags = _annotation is null ? string.Empty : string.Join(", ", _annotation.Tags);
        Notes = _annotation?.Notes ?? string.Empty;
        HasDisplayed = Series?.HasGenerated == true;
        HasSensors = Series?.Sensors.Count > 0;
        DecimationNote = Series is null ? string.Empty : string.Format(CultureInfo.CurrentCulture, Strings.Chart_Decimated_Format, Series.Presents, Math.Min(Series.Presents, Decimator.DefaultBuckets * 2));
        PresentStats(row);
        PresentChips();
        Presented?.Invoke(this, EventArgs.Empty);
    }

    private void PresentStats(SessionRow row)
    {
        string? presented = IsHooked && FpsPresentation.FromRow(row).Kind == FpsReadoutKind.Presented ? Strings.Summary_Lows_Presented : null;
        Stats.Clear();
        Stats.Add(new StatCardModel(Strings.Summary_Stat_Avg, Readout.Line));
        Stats.Add(new StatCardModel(Strings.Summary_Stat_Median, IsHooked ? Formats.Fps(row.MedianFps) : Strings.Common_NotAvailable));
        Stats.Add(new StatCardModel(Strings.Summary_Stat_P1Low, IsHooked ? Formats.Fps(row.P1LowFps) : Strings.Common_NotAvailable, presented));
        Stats.Add(new StatCardModel(Strings.Summary_Stat_P01Low, IsHooked ? Formats.Fps(row.P01LowFps) : Strings.Common_NotAvailable, presented));
        Stats.Add(new StatCardModel(Strings.Summary_Stat_MinMax, IsHooked ? string.Format(CultureInfo.CurrentCulture, Strings.Summary_Stat_MinMax_Format, Formats.Fps(row.MinFps), Formats.Fps(row.MaxFps)) : Strings.Common_NotAvailable));
        Stats.Add(new StatCardModel(Strings.Summary_Stat_StdDev, IsHooked && row.FrametimeStdDevMs is double sd ? string.Format(CultureInfo.CurrentCulture, Strings.Summary_Stat_StdDev_Format, sd) : Strings.Common_NotAvailable));
        Stats.Add(new StatCardModel(Strings.Summary_Stat_Stutter, IsHooked && row.StutterCount is long count && row.StutterTimePct is double pct
            ? string.Format(CultureInfo.CurrentCulture, Strings.Summary_Stat_Stutter_Format, count, pct) : Strings.Common_NotAvailable));
        Stats.Add(new StatCardModel(Strings.Summary_Stat_Duration, Formats.Duration(row.DurationSeconds)));
    }

    private void PresentChips()
    {
        Chips.Clear();
        foreach (TriStateKind kind in new[] { TriStateKind.RayTracing, TriStateKind.PathTracing, TriStateKind.RayReconstruction })
        {
            Chips.Add(TriStateChipModel.Of(kind, TriStateResolution.Resolve(kind, Row!, _annotation, Game!)));
        }
    }

    private static string Sanitise(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string([.. name.Select(c => invalid.Contains(c) ? '_' : c)]);
    }
}

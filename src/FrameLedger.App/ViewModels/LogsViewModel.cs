using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.App.Services;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// Logs (08_UI §Logs, <c>10_LOGGING</c> §In-app log viewer): the newest <c>ui-*.log</c> or <c>agent-*.log</c> tailed
/// with shared read (≤ 2 MB), a level filter, a text search, pause, "Open logs folder" and "Export bug bundle".
/// The tail refreshes on a timer while the page is shown (<see cref="Start"/>/<see cref="Stop"/> from the page's
/// Loaded/Unloaded) and never while paused; the read runs off the UI thread and the result lands on it.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed partial class LogsViewModel : ObservableObject, IDisposable
{
    public static readonly TimeSpan RefreshPeriod = TimeSpan.FromSeconds(2);

    private readonly LogTail _tail;
    private readonly BugBundleBuilder _bundles;
    private readonly IFileSaver _saver;
    private readonly IAgentRequests _agent;
    private readonly IMessageStrip _strip;
    private readonly TimeProvider _clock;
    private readonly UiThread _ui = new();
    private IReadOnlyList<string> _lines = [];
    private ITimer? _timer;

    [ObservableProperty]
    private LogSource _source = LogSource.Ui;

    [ObservableProperty]
    private LogLevelFilter _level = LogLevelFilter.All;

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private string _status = Strings.Logs_Empty;

    [ObservableProperty]
    private string? _currentFile;

    public LogsViewModel(LogTail tail, BugBundleBuilder bundles, IFileSaver saver, IAgentRequests agent, IMessageStrip strip, TimeProvider? clock = null)
    {
        _tail = tail ?? throw new ArgumentNullException(nameof(tail));
        _bundles = bundles ?? throw new ArgumentNullException(nameof(bundles));
        _saver = saver ?? throw new ArgumentNullException(nameof(saver));
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _strip = strip ?? throw new ArgumentNullException(nameof(strip));
        _clock = clock ?? TimeProvider.System;
        Pending = RefreshAsync();
    }

    public static string Header => Strings.Logs_Header;

    public static IReadOnlyList<Choice<LogSource>> Sources { get; } =
    [
        new(LogSource.Ui, Strings.Logs_Source_Ui),
        new(LogSource.Agent, Strings.Logs_Source_Agent),
    ];

    public static IReadOnlyList<Choice<LogLevelFilter>> Levels { get; } =
    [
        new(LogLevelFilter.All, Strings.Logs_Level_All),
        new(LogLevelFilter.WarningAndAbove, Strings.Logs_Level_Warning),
        new(LogLevelFilter.ErrorAndAbove, Strings.Logs_Level_Error),
    ];

    /// <summary>Lines shown after the filters.</summary>
    public int ShownCount { get; private set; }

    /// <summary>Lines in the tail before the filters.</summary>
    public int TotalCount => _lines.Count;

    /// <summary>A pending refresh or export, for a test to await; the UI does not.</summary>
    public Task Pending { get; private set; } = Task.CompletedTask;

    /// <summary>Starts the periodic refresh (the page's Loaded).</summary>
    public void Start()
    {
        _timer?.Dispose();
        _timer = _clock.CreateTimer(_ => _ui.Post(Tick), null, RefreshPeriod, RefreshPeriod);
    }

    /// <summary>Stops it (the page's Unloaded) — a page that is not shown reads nothing.</summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Stop();

    public Task RefreshAsync() => Pending = RefreshCoreAsync();

    partial void OnSourceChanged(LogSource value) => Pending = RefreshCoreAsync();

    partial void OnLevelChanged(LogLevelFilter value) => Apply();

    partial void OnSearchChanged(string value) => Apply();

    [RelayCommand]
    private static void OpenFolder()
    {
        try
        {
            using Process? _ = Process.Start(new ProcessStartInfo("explorer.exe", UiPaths.Logs) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Serilog.Log.Warning(ex, "ui: could not open the logs folder");
        }
    }

    /// <summary><c>10_LOGGING</c> §Bug report flow step 2: the zip, where the user says; nothing is sent (steps 3–4 are P4's).</summary>
    [RelayCommand]
    private Task ExportBundleAsync() => Pending = ExportBundleCoreAsync();

    private void Tick()
    {
        if (!IsPaused)
        {
            Pending = RefreshCoreAsync();
        }
    }

    private async Task RefreshCoreAsync()
    {
        LogSource source = Source;
        (string? file, IReadOnlyList<string> lines) = await Task.Run(() =>
        {
            string? newest = _tail.NewestFile(source);
            return (newest, newest is null ? [] : LogTail.ReadLines(newest));
        }).ConfigureAwait(true);
        CurrentFile = file;
        _lines = lines;
        Apply();
    }

    private void Apply()
    {
        IReadOnlyList<string> shown = LogTail.Filter(_lines, Level, Search);
        ShownCount = shown.Count;
        Text = string.Join(Environment.NewLine, shown);
        Status = CurrentFile is null
            ? Strings.Logs_Empty
            : string.Format(CultureInfo.CurrentCulture, Strings.Logs_Showing_Format, ShownCount, TotalCount, Path.GetFileName(CurrentFile));
        OnPropertyChanged(nameof(ShownCount));
        OnPropertyChanged(nameof(TotalCount));
    }

    private async Task ExportBundleCoreAsync()
    {
        DateTimeOffset now = _clock.GetUtcNow();
        string? path = _saver.PickSavePath("Zip archive (*.zip)|*.zip", BugBundleBuilder.SuggestedName(now));
        if (path is null)
        {
            return;
        }

        try
        {
            IReadOnlyList<string> written = await _bundles.WriteAsync(path, _agent.Hello).ConfigureAwait(true);
            _strip.Success(Strings.Logs_ExportBundle, string.Format(CultureInfo.CurrentCulture, Strings.Logs_Bundle_Exported_Format, path, written.Count));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Serilog.Log.Warning(ex, "ui: the bug bundle could not be written");
            _strip.Warn(Strings.Logs_ExportBundle, string.Format(CultureInfo.CurrentCulture, Strings.Logs_Bundle_Failed_Format, ex.Message));
        }
    }
}

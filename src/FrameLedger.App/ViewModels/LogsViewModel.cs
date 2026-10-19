// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.ComponentModel;
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
public sealed partial class LogsViewModel : ObservableObject
{
    public static readonly TimeSpan RefreshPeriod = TimeSpan.FromSeconds(2);

    private readonly LogTail _tail;
    private readonly BugReportFlow _bugReports;
    private readonly TimeProvider _clock;
    private readonly IMessageStrip _strip;
    private readonly UiThread _ui = new();
    private IReadOnlyList<string> _lines = [];
    private ITimer? _timer;

    // What the last read read (beta.23): a tick that finds the newest file unchanged reads nothing and redraws nothing.
    private (string? File, long Length, DateTime WrittenUtc) _read;

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

    public LogsViewModel(LogTail tail, BugReportFlow bugReports, IMessageStrip strip, TimeProvider? clock = null)
    {
        _tail = tail ?? throw new ArgumentNullException(nameof(tail));
        _bugReports = bugReports ?? throw new ArgumentNullException(nameof(bugReports));
        _strip = strip ?? throw new ArgumentNullException(nameof(strip));
        _clock = clock ?? TimeProvider.System;
        Pending = RefreshAsync();
    }

    public static string Header => Strings.Logs_Header;

    /// <summary>The two logs, labelled in the language this page opened in (beta.15: a static list kept the first language).</summary>
    public IReadOnlyList<Choice<LogSource>> Sources { get; } =
    [
        new(LogSource.Ui, Strings.Logs_Source_Ui),
        new(LogSource.Agent, Strings.Logs_Source_Agent),
    ];

    public IReadOnlyList<Choice<LogLevelFilter>> Levels { get; } =
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

    /// <summary>
    /// Stops it (the page's Unloaded) — a page that is not shown reads nothing. Not <see cref="IDisposable"/> (beta.23): the
    /// root container kept every transient disposable it made, so every Logs page ever opened stayed in memory with its
    /// up-to-2-MB tail, twice (the lines and the text).
    /// </summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public Task RefreshAsync() => Pending = RefreshCoreAsync();

    partial void OnSourceChanged(LogSource value) => Pending = RefreshCoreAsync();

    partial void OnLevelChanged(LogLevelFilter value) => Apply();

    partial void OnSearchChanged(string value) => Apply();

    [RelayCommand]
    private static void OpenFolder()
    {
        try
        {
            Explorer.OpenFolder(UiPaths.Logs);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Serilog.Log.Warning(ex, "ui: could not open the logs folder");
        }
    }

    /// <summary><c>10_LOGGING</c> §Bug report flow: the zip where the user says, the preview, then the issue form or the clipboard — nothing is ever sent by itself.</summary>
    [RelayCommand]
    private Task ExportBundleAsync() => Pending = ExportBundleCoreAsync();

    private void Tick()
    {
        if (!IsPaused)
        {
            Pending = RefreshIfChangedAsync();
        }
    }

    /// <summary>What a tick does: the tail again only when the newest file changed since the last read.</summary>
    internal Task RefreshIfChangedAsync() => RefreshCoreAsync(unlessUnchanged: true);

    /// <summary>
    /// Reads the newest file's tail and shows it through the filters. A tick (<paramref name="unlessUnchanged"/>) whose file
    /// has the length and the write time of the last read stops there (beta.23): it re-read up to 2 MB and replaced the whole
    /// text every two seconds while nothing had been written. The filtering and the joining happen off the UI thread too.
    /// </summary>
    private async Task RefreshCoreAsync(bool unlessUnchanged = false)
    {
        LogSource source = Source;
        LogLevelFilter level = Level;
        string search = Search;
        (string? File, long Length, DateTime WrittenUtc) last = _read;
        (bool unchanged, (string? File, long Length, DateTime WrittenUtc) read, IReadOnlyList<string> lines, IReadOnlyList<string> shown, string text) = await Task.Run(() =>
        {
            string? newest = _tail.NewestFile(source);
            (string? File, long Length, DateTime WrittenUtc) stamp = Stamp(newest);
            if (unlessUnchanged && stamp.File is not null && stamp == last)
            {
                return (true, stamp, (IReadOnlyList<string>)[], (IReadOnlyList<string>)[], string.Empty);
            }

            IReadOnlyList<string> all = newest is null ? [] : LogTail.ReadLines(newest);
            IReadOnlyList<string> filtered = LogTail.Filter(all, level, search);
            return (false, stamp, all, filtered, string.Join(Environment.NewLine, filtered));
        }).ConfigureAwait(true);
        if (unchanged)
        {
            return;
        }

        _read = read;
        CurrentFile = read.File;
        _lines = lines;
        if (level == Level && string.Equals(search, Search, StringComparison.Ordinal))
        {
            Show(shown, text);
        }
        else
        {
            Apply();   // the filters changed while the file was read
        }
    }

    private static (string? File, long Length, DateTime WrittenUtc) Stamp(string? file)
    {
        if (file is null)
        {
            return (null, 0, default);
        }

        try
        {
            var info = new FileInfo(file);
            return info.Exists ? (file, info.Length, info.LastWriteTimeUtc) : (file, -1, default);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (file, -1, default);
        }
    }

    private void Apply()
    {
        IReadOnlyList<string> shown = LogTail.Filter(_lines, Level, Search);
        Show(shown, string.Join(Environment.NewLine, shown));
    }

    private void Show(IReadOnlyList<string> shown, string text)
    {
        ShownCount = shown.Count;
        Text = text;
        Status = CurrentFile is null
            ? Strings.Logs_Empty
            : string.Format(CultureInfo.CurrentCulture, Strings.Logs_Showing_Format, ShownCount, TotalCount, Path.GetFileName(CurrentFile));
        OnPropertyChanged(nameof(ShownCount));
        OnPropertyChanged(nameof(TotalCount));
    }

    /// <summary><c>10_LOGGING</c> §Bug report flow steps 2–4 (P4 PR-3), the same flow Help ▸ Report a bug runs.</summary>
    private async Task ExportBundleCoreAsync() => _ = await _bugReports.RunAsync().ConfigureAwait(true);
}

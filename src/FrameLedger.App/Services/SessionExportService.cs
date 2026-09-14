using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
using FrameLedger.App.Charts;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.App.Services;

/// <summary>
/// File ▸ Export ▸ Session CSV / Session JSON for a session id (P4 PR-3): the same five loads and the same two
/// <see cref="SessionExporter"/> writers the summary window uses, reachable from the shell for the session the user
/// last selected. The chart PNG is not here — it needs the summary window's live plot, so the shell opens that.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime; a cached CompositeFormat would pin the first culture")]
public sealed class SessionExportService
{
    private readonly ISessionRepository _sessions;
    private readonly IGameRepository _games;
    private readonly ISessionAnnotationRepository _annotations;
    private readonly IHardwareSnapshotRepository _hardware;
    private readonly SessionSeriesLoader _loader;
    private readonly IFileSaver _saver;
    private readonly IMessageStrip _strip;

    public SessionExportService(ISessionRepository sessions, IGameRepository games, ISessionAnnotationRepository annotations,
        IHardwareSnapshotRepository hardware, SessionSeriesLoader loader, IFileSaver saver, IMessageStrip strip)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _annotations = annotations ?? throw new ArgumentNullException(nameof(annotations));
        _hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _saver = saver ?? throw new ArgumentNullException(nameof(saver));
        _strip = strip ?? throw new ArgumentNullException(nameof(strip));
    }

    public Task<string?> ExportCsvAsync(long sessionId, CancellationToken ct = default) => ExportAsync(sessionId, "csv", "CSV|*.csv", ct);

    public Task<string?> ExportJsonAsync(long sessionId, CancellationToken ct = default) => ExportAsync(sessionId, "json", "JSON|*.json", ct);

    /// <summary>The suggested file name the summary window uses too: the game, the local start time, the extension.</summary>
    public static string SuggestedName(GameRow game, SessionRow row, string extension)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(row);
        string name = string.Concat(game.Name.Select(static c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
        return (name.Length == 0 ? "session" : name) + "-" + row.StartedAt.ToLocalTime().ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + "." + extension;
    }

    /// <summary>The path written, or null when the session was not found or the user cancelled.</summary>
    private async Task<string?> ExportAsync(long sessionId, string extension, string filter, CancellationToken ct)
    {
        SessionRow? row = await _sessions.FindByIdAsync(sessionId, ct).ConfigureAwait(true);
        GameRow? game = row is null ? null : await _games.FindByIdAsync(row.GameId, ct).ConfigureAwait(true);
        if (row is null || game is null)
        {
            _strip.Warn(Strings.Menu_File_Export, Strings.Export_NoSelection_Body);
            return null;
        }

        string? path = _saver.PickSavePath(filter, SuggestedName(game, row, extension));
        if (path is null)
        {
            return null;
        }

        SessionAnnotation? annotation = await _annotations.FindAsync(sessionId, ct).ConfigureAwait(true);
        IReadOnlyList<SegmentRow> segments = await _sessions.FindSegmentsAsync(sessionId, ct).ConfigureAwait(true);
        HardwareSnapshot? snapshot = await _hardware.FindAsync(row.SnapshotId, ct).ConfigureAwait(true);
        SessionSeries? series = string.Equals(extension, "csv", StringComparison.Ordinal) && row.Tier == CaptureTier.Hooked
            ? await _loader.LoadAsync(sessionId, ct).ConfigureAwait(true)
            : null;

        try
        {
            await Task.Run(() =>
            {
                if (string.Equals(extension, "csv", StringComparison.Ordinal))
                {
                    using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
                    SessionExporter.WriteCsv(writer, row, game, snapshot, segments, series);
                }
                else
                {
                    using FileStream stream = File.Create(path);
                    SessionExporter.WriteJson(stream, SessionExporter.Document(row, game, snapshot, segments, annotation));
                }
            }, ct).ConfigureAwait(true);
            _strip.Success(Strings.Menu_File_Export, string.Format(CultureInfo.CurrentCulture, Strings.Summary_Exported_Format, path));
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _strip.Warn(Strings.Menu_File_Export, string.Format(CultureInfo.CurrentCulture, Strings.Summary_Export_Failed_Format, ex.Message));
            return null;
        }
    }
}

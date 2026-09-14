using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.App.Services;
using FrameLedger.Application.Settings;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Shared.Ipc;
using Microsoft.Data.Sqlite;
using Serilog;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// Tools ▸ Database maintenance (P4 PR-7, <c>06_DATA_MODEL</c> §Retention): the integrity check and the backup, which
/// only read; compaction (<c>VACUUM</c>), refused while the Agent reports a session because a session writes; and the
/// retention sweep, which the Agent runs when asked because the raw series are its rows (§Writer ownership). Each
/// action ends in one line in <see cref="Status"/>; a failure is a line too, never an unhandled command.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime; a cached CompositeFormat would pin the first culture")]
public sealed partial class DatabaseMaintenanceViewModel : ObservableObject
{
    /// <summary>SQLite's busy result: another connection held the lock past the busy timeout.</summary>
    private const int _sqliteBusy = 5;

    private readonly LedgerMaintenance _ledger;
    private readonly string _ledgerPath;
    private readonly IAgentRequests _agent;
    private readonly IFileSaver _files;
    private readonly IDatabaseMaintenancePrompts _prompts;
    private readonly RegisteredSettings _settings;
    private readonly TimeProvider _clock;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckIntegrityCommand), nameof(BackUpCommand), nameof(SweepCommand), nameof(CompactCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = Strings.Maintenance_Status_Ready;

    public DatabaseMaintenanceViewModel(LedgerMaintenance ledger, string ledgerPath, IAgentRequests agent, IFileSaver files, IDatabaseMaintenancePrompts prompts,
        RegisteredSettings settings, TimeProvider? clock = null)
    {
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        ArgumentException.ThrowIfNullOrWhiteSpace(ledgerPath);
        _ledgerPath = Path.GetFullPath(ledgerPath);
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _clock = clock ?? TimeProvider.System;
    }

    private bool CanRun => !IsBusy;

    /// <summary><c>PRAGMA integrity_check</c>: "no problems", or the count and SQLite's first line.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task CheckIntegrityAsync() => RunAsync(async () =>
    {
        IReadOnlyList<string> lines = await _ledger.CheckIntegrityAsync().ConfigureAwait(true);
        Status = LedgerMaintenance.IsHealthy(lines)
            ? Strings.Maintenance_Integrity_Ok
            : string.Format(CultureInfo.CurrentCulture, Strings.Maintenance_Integrity_Problems_Format, lines.Count, lines.Count > 0 ? lines[0] : string.Empty);
    });

    /// <summary>A consistent copy through <c>VACUUM INTO</c> to a file the user names; never the live ledger itself.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task BackUpAsync() => RunAsync(async () =>
    {
        string suggested = string.Format(CultureInfo.InvariantCulture, "FrameLedger-ledger-{0:yyyyMMdd-HHmmss}.db", _clock.GetLocalNow());
        string? path = _files.PickSavePath(Strings.Maintenance_Backup_Filter, suggested);
        if (path is null)
        {
            return;
        }

        string full = Path.GetFullPath(path);
        if (IsLedgerFile(full))
        {
            Status = Strings.Maintenance_Backup_IsLedger;
            return;
        }

        // The save dialog already asked before an existing file is replaced; VACUUM INTO refuses to write over one.
        if (File.Exists(full))
        {
            File.Delete(full);
        }

        await _ledger.BackupAsync(full).ConfigureAwait(true);
        Status = string.Format(CultureInfo.CurrentCulture, Strings.Maintenance_Backup_Done_Format, full, Megabytes(new FileInfo(full).Length));
    });

    /// <summary>
    /// The retention sweep, asked of the Agent: nothing to do when retention is unlimited, a confirmation before rows
    /// go, and the Agent's own count after.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task SweepAsync() => RunAsync(async () =>
    {
        int keep = await _settings.GetIntegerAsync(SettingsRegistry.RetentionRawSessionsPerGame).ConfigureAwait(true);
        if (keep == 0)
        {
            Status = Strings.Maintenance_Sweep_Unlimited;
            return;
        }

        if (!_agent.IsConnected)
        {
            Status = Strings.Maintenance_Sweep_NoAgent;
            return;
        }

        if (!await _prompts.ConfirmSweepAsync(keep).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            IpcEnvelope envelope = await _agent.RequestAsync(IpcMessageType.SweepRetention, new SweepRetentionRequest()).ConfigureAwait(true);
            SweepRetentionAck ack = IpcCodec.Payload<SweepRetentionAck>(envelope) ?? throw new InvalidOperationException("SweepRetention answered without a payload");
            Status = string.Format(CultureInfo.CurrentCulture, Strings.Maintenance_Sweep_Done_Format, ack.Sessions, ack.Games, ack.Keep);
        }
        catch (IpcRequestException ex) when (string.Equals(ex.Code, IpcErrorCode.UnknownType, StringComparison.Ordinal))
        {
            Log.Warning("maintenance: the Agent does not know SweepRetention ({Message})", ex.Message);
            Status = Strings.Maintenance_Sweep_AgentTooOld;
        }
    });

    /// <summary><c>VACUUM</c>, only while the Agent says no session runs; "busy" is a line, not a crash.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task CompactAsync() => RunAsync(async () =>
    {
        if (await IsSessionRunningAsync().ConfigureAwait(true))
        {
            Status = Strings.Maintenance_Compact_SessionRunning;
            return;
        }

        LedgerCompaction result = await _ledger.CompactAsync().ConfigureAwait(true);
        Status = string.Format(CultureInfo.CurrentCulture, Strings.Maintenance_Compact_Done_Format, Megabytes(result.BytesBefore), Megabytes(result.BytesAfter));
    });

    /// <summary>A fresh <c>GetStatus</c> when connected (the connection's cached one can be a minute old); no Agent, no session.</summary>
    private async Task<bool> IsSessionRunningAsync()
    {
        if (!_agent.IsConnected)
        {
            return false;
        }

        IpcEnvelope envelope = await _agent.RequestAsync(IpcMessageType.GetStatus, new GetStatusRequest()).ConfigureAwait(true);
        StatusAck? status = IpcCodec.Payload<StatusAck>(envelope);
        return status is null || !string.Equals(status.State, "idle", StringComparison.Ordinal) || status.ActiveSessions.Count > 0;
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        Status = Strings.Maintenance_Status_Working;
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == _sqliteBusy)
        {
            Log.Warning(ex, "maintenance: the ledger was busy");
            Status = Strings.Maintenance_Busy;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException or IpcRequestException or TimeoutException or InvalidOperationException)
        {
            Log.Warning(ex, "maintenance: the action failed");
            Status = string.Format(CultureInfo.CurrentCulture, Strings.Maintenance_Failed_Format, ex.Message);
        }
        finally
        {
            if (string.Equals(Status, Strings.Maintenance_Status_Working, StringComparison.Ordinal))
            {
                Status = Strings.Maintenance_Status_Ready;
            }

            IsBusy = false;
        }
    }

    private bool IsLedgerFile(string full) =>
        string.Equals(full, _ledgerPath, StringComparison.OrdinalIgnoreCase)
        || full.StartsWith(_ledgerPath + "-", StringComparison.OrdinalIgnoreCase);

    private static string Megabytes(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.CurrentCulture);
}

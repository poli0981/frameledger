using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FrameLedger.App.Dialogs;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Settings;
using FrameLedger.Infrastructure.Persistence;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>
/// <see cref="IDatabaseMaintenancePrompts"/> in WPF UI: a <c>ContentDialog</c> over a fresh
/// <see cref="DatabaseMaintenanceViewModel"/> each time it opens, and a <c>MessageBox</c> with a Danger button for the sweep.
/// </summary>
public sealed class DatabaseMaintenancePrompts : IDatabaseMaintenancePrompts
{
    private readonly IContentDialogService _dialogs;
    private readonly LedgerDatabase _db;
    private readonly IAgentRequests _agent;
    private readonly IFileSaver _files;
    private readonly RegisteredSettings _settings;

    public DatabaseMaintenancePrompts(IContentDialogService dialogs, LedgerDatabase db, IAgentRequests agent, IFileSaver files, RegisteredSettings settings)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task ShowAsync(CancellationToken ct = default)
    {
        var viewModel = new DatabaseMaintenanceViewModel(new LedgerMaintenance(_db), _db.Path, _agent, _files, this, _settings);
        var dialog = new ContentDialog
        {
            Title = Strings.Maintenance_Title,
            Content = new DatabaseMaintenanceContent(viewModel),
            CloseButtonText = Strings.Common_Close,
            DialogMaxWidth = 760,
        };
        _ = await _dialogs.ShowAsync(dialog, ct).ConfigureAwait(true);
    }

    [SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture, which changes at runtime")]
    public async Task<bool> ConfirmSweepAsync(int keep, CancellationToken ct = default)
    {
        var box = new MessageBox
        {
            Title = Strings.Maintenance_Sweep_Confirm_Title,
            Content = string.Format(CultureInfo.CurrentCulture, Strings.Maintenance_Sweep_Confirm_Body_Format, keep),
            PrimaryButtonText = Strings.Maintenance_Sweep_Confirm_Button,
            PrimaryButtonAppearance = ControlAppearance.Danger,
            CloseButtonText = Strings.Common_Cancel,
        };
        MessageBoxResult result = await box.ShowDialogAsync(cancellationToken: ct).ConfigureAwait(true);
        return result == MessageBoxResult.Primary;
    }
}

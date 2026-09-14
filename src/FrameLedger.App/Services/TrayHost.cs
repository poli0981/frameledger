using System.ComponentModel;
using System.Windows.Controls;
using FrameLedger.App.ViewModels;
using H.NotifyIcon;
using Serilog;

namespace FrameLedger.App.Services;

/// <summary>
/// The tray icon (H.NotifyIcon, CLAUDE.md stack table; 08_UI §Shell) over <see cref="TrayViewModel"/>: the icon
/// and tooltip follow the state, the context menu is the four items, a left click opens the window, and the
/// FR-3.8 balloon's click opens the saved session. Created once the shell is up, on the UI thread.
/// </summary>
public sealed class TrayHost : IDisposable
{
    private readonly TrayViewModel _viewModel;
    private readonly WindowClosePolicy _policy;
    private TaskbarIcon? _icon;
    private System.Drawing.Icon? _image;

    public TrayHost(TrayViewModel viewModel, WindowClosePolicy policy)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public bool IsCreated => _icon is not null;

    public void Create()
    {
        if (_icon is not null)
        {
            return;
        }

        try
        {
            var menu = new ContextMenu();
            menu.Items.Add(new MenuItem { Header = Strings.Tray_Open, Command = _viewModel.OpenCommand });
            var pause = new MenuItem { Header = _viewModel.PauseText, Command = _viewModel.TogglePauseCommand };
            menu.Items.Add(pause);
            menu.Items.Add(new MenuItem { Header = Strings.Tray_AgentStatus, Command = _viewModel.AgentStatusCommand });
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = Strings.Tray_Exit, Command = _viewModel.ExitCommand });

            _image = TrayIcons.For(_viewModel.State);
            var icon = new TaskbarIcon
            {
                Icon = _image,
                ToolTipText = _viewModel.Tooltip,
                ContextMenu = menu,
                LeftClickCommand = _viewModel.OpenCommand,
                NoLeftClickDelay = true,
            };
            icon.TrayBalloonTipClicked += (_, _) => _viewModel.OpenLastSavedCommand.Execute(null);
            icon.ForceCreate();
            _icon = icon;
            _viewModel.PropertyChanged += (_, e) => OnViewModelChanged(pause, e);
            _viewModel.ToastRequested += OnToast;
            _policy.TrayAvailable = true;
            Log.Information("tray: created");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // No tray is a missing convenience, not a broken app: the window stays, closing it exits.
            Log.Warning(ex, "tray: could not be created; closing the window exits");
            _policy.TrayAvailable = false;
        }
    }

    public void Dispose()
    {
        _policy.TrayAvailable = false;
        _viewModel.ToastRequested -= OnToast;
        _icon?.Dispose();
        _icon = null;
        _image?.Dispose();
        _image = null;
    }

    private void OnViewModelChanged(MenuItem pause, PropertyChangedEventArgs e)
    {
        if (_icon is null)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(TrayViewModel.State):
                System.Drawing.Icon previous = _image!;
                _image = TrayIcons.For(_viewModel.State);
                _icon.Icon = _image;
                previous.Dispose();
                break;
            case nameof(TrayViewModel.Tooltip):
                _icon.ToolTipText = _viewModel.Tooltip;
                break;
            case nameof(TrayViewModel.PauseText):
                pause.Header = _viewModel.PauseText;
                break;
            default:
                break;
        }
    }

    private void OnToast(object? sender, TrayToastEventArgs toast)
    {
        try
        {
            _icon?.ShowNotification(toast.Title, toast.Body);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "tray: the balloon could not be shown");
        }
    }
}

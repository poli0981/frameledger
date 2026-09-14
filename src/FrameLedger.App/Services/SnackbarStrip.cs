using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary><see cref="IMessageStrip"/> over the shell's <see cref="ISnackbarService"/>.</summary>
public sealed class SnackbarStrip : IMessageStrip
{
    private static readonly TimeSpan _duration = TimeSpan.FromSeconds(4);
    private readonly ISnackbarService _snackbar;

    public SnackbarStrip(ISnackbarService snackbar) => _snackbar = snackbar ?? throw new ArgumentNullException(nameof(snackbar));

    public void Info(string title, string body) => _snackbar.Show(title, body, ControlAppearance.Secondary, new SymbolIcon(SymbolRegular.Info24), _duration);

    public void Success(string title, string body) => _snackbar.Show(title, body, ControlAppearance.Success, new SymbolIcon(SymbolRegular.Checkmark24), _duration);

    public void Warn(string title, string body) => _snackbar.Show(title, body, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Warning24), _duration);
}

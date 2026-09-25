using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.App.Services;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// One row of Settings ▸ Capture's user-mode exception list (D33): the game, what was found, the evidence, the state — and
/// the one action the state allows, making the exception (through its disclosure) or withdrawing it.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed partial class ExceptionGameViewModel
{
    private readonly Func<ExceptionGameViewModel, Task> _grant;
    private readonly Func<ExceptionGameViewModel, Task> _withdraw;

    public ExceptionGameViewModel(long gameId, string name, ExceptionView view, Func<ExceptionGameViewModel, Task> grant, Func<ExceptionGameViewModel, Task> withdraw)
    {
        ArgumentNullException.ThrowIfNull(view);
        GameId = gameId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        View = view;
        _grant = grant ?? throw new ArgumentNullException(nameof(grant));
        _withdraw = withdraw ?? throw new ArgumentNullException(nameof(withdraw));
        FindingText = string.Format(CultureInfo.CurrentCulture, Strings.Exception_Finding_Format, view.Family ?? Strings.Common_NotAvailable, view.Signal ?? Strings.Common_NotAvailable);
        SessionsText = string.Format(CultureInfo.CurrentCulture, Strings.Exception_Sessions_Format, view.Sessions);
    }

    public long GameId { get; }

    public string Name { get; }

    public ExceptionView View { get; }

    public string FindingText { get; }

    public string SessionsText { get; }

    public string StateText => View.Text;

    public string? LapseText => View.LapseText;

    public bool CanGrant => View.CanGrant;

    public bool CanWithdraw => View.CanWithdraw;

    [RelayCommand]
    private Task GrantAsync() => _grant(this);

    [RelayCommand]
    private Task WithdrawAsync() => _withdraw(this);
}

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FrameLedger.App.Services;
using SafetyStrings = FrameLedger.Shared.Strings;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// D33's disclosure content (<c>19_SAFETY</c> §The user-mode exception): why the exception is offered, the risk at the same
/// grain as FR-2.1's dialog, what is still refused, how it ends, and that it turns nothing on. <see cref="Accepted"/> — the
/// user ticking "I accept the risk of a ban for this game" — is the one thing that enables the dialog's primary button.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime; a cached CompositeFormat would pin the first culture")]
public sealed partial class AntiCheatExceptionDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _accepted;

    public AntiCheatExceptionDialogViewModel(AntiCheatExceptionFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        Facts = facts;
        Title = string.Format(CultureInfo.CurrentCulture, SafetyStrings.Safety_Exception_Title_Format, facts.GameName);
        Intro = string.Format(CultureInfo.CurrentCulture, SafetyStrings.Safety_Exception_Intro_Format, facts.Family, facts.Signal);
        Why = string.Format(CultureInfo.CurrentCulture, SafetyStrings.Safety_Exception_Why_Format, facts.Family, facts.Sessions);
        Risk = string.Format(CultureInfo.CurrentCulture, SafetyStrings.Safety_Exception_Risk_Format, facts.Family);
        Still = string.Format(CultureInfo.CurrentCulture, SafetyStrings.Safety_Exception_Still_Format, facts.Family);
    }

    public AntiCheatExceptionFacts Facts { get; }

    public string Title { get; }

    public string Intro { get; }

    public string Why { get; }

    public string Risk { get; }

    public string Still { get; }

    public static string Ends => SafetyStrings.Safety_Exception_Ends;

    public static string Next => SafetyStrings.Safety_Exception_Next;

    public static string Online => SafetyStrings.Safety_Exception_Online;

    public static string AcceptText => SafetyStrings.Safety_Exception_Accept;

    public static string GrantText => SafetyStrings.Safety_Exception_Grant;

    public static string CancelText => SafetyStrings.Safety_Exception_Cancel;
}

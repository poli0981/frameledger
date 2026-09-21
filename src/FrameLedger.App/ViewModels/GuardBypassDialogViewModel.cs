using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FrameLedger.Shared.Safety;
using SafetyStrings = FrameLedger.Shared.Strings;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// The guard-bypass disclosure's state (owner decision 2026-09-21): two independent acts, both required — a ticked
/// acknowledgement and the typed phrase — so neither a stray click nor a held Enter key can overrule the guard. Text
/// from <c>FrameLedger.Shared</c>'s <c>Safety_Bypass_*</c> only, reviewed as the safety text it is.
/// </summary>
public sealed partial class GuardBypassDialogViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfirmed))]
    private bool _acknowledged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfirmed))]
    private string _typed = string.Empty;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime; a cached CompositeFormat would pin the first culture")]
    public GuardBypassDialogViewModel(string gameName, string? finding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);
        Title = string.Format(CultureInfo.CurrentCulture, SafetyStrings.Safety_Bypass_Title_Format, gameName);
        Found = string.IsNullOrWhiteSpace(finding)
            ? SafetyStrings.Safety_Bypass_Found_Nothing
            : string.Format(CultureInfo.CurrentCulture, SafetyStrings.Safety_Bypass_Found_Format, finding);
        TypeToConfirm = string.Format(CultureInfo.CurrentCulture, SafetyStrings.Safety_Bypass_TypeToConfirm_Format, GuardBypassDisclosure.ConfirmationPhrase);
    }

    public string Title { get; }

    public string Found { get; }

    public string TypeToConfirm { get; }

    public static string Intro => SafetyStrings.Safety_Bypass_Intro;

    public static string Risk => SafetyStrings.Safety_Bypass_Risk;

    public static string Liability => SafetyStrings.Safety_Bypass_Liability;

    public static string Recorded => SafetyStrings.Safety_Bypass_Recorded;

    public static string CheckboxText => SafetyStrings.Safety_Bypass_Checkbox;

    public static string Phrase => GuardBypassDisclosure.ConfirmationPhrase;

    public static string ConfirmText => SafetyStrings.Safety_Bypass_Confirm;

    public static string KeepGuardText => SafetyStrings.Safety_Bypass_KeepGuard;

    /// <summary>Both acts, or nothing: the tick alone and the phrase alone each leave the primary button disabled.</summary>
    public bool IsConfirmed => Acknowledged && IsThePhrase(Typed);

    /// <summary>Exact and case-sensitive after trimming: "bypass" is not "BYPASS".</summary>
    public static bool IsThePhrase(string? typed) => string.Equals(typed?.Trim(), GuardBypassDisclosure.ConfirmationPhrase, StringComparison.Ordinal);
}

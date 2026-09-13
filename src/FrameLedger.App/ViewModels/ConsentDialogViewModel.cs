using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SafetyStrings = FrameLedger.Shared.Strings;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// FR-2.1's dialog content: the reviewed statements (<c>19_SAFETY</c> §User-facing consent — both tiers at the
/// same grain, recommending neither) and the typed acknowledgement. <see cref="IsAcknowledged"/> is the one
/// thing that enables the dialog's primary button: the phrase, exactly, in the UI's language.
/// </summary>
public sealed partial class ConsentDialogViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAcknowledged))]
    private string _typed = string.Empty;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime; a cached CompositeFormat would pin the first culture")]
    public ConsentDialogViewModel(string gameName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);
        GameName = gameName;
        Title = string.Format(CultureInfo.CurrentCulture, SafetyStrings.Safety_Consent_Title_Format, gameName);
        TypeToConfirm = string.Format(CultureInfo.CurrentCulture, SafetyStrings.Safety_Consent_TypeToConfirm_Format, SafetyStrings.Safety_Consent_Phrase);
    }

    public string GameName { get; }

    public string Title { get; }

    public string TypeToConfirm { get; }

    public static string Intro => SafetyStrings.Safety_Consent_Intro;

    public static string Injected => SafetyStrings.Safety_Consent_Injected;

    public static string Hooked => SafetyStrings.Safety_Consent_Hooked;

    public static string NotHooked => SafetyStrings.Safety_Consent_NotHooked;

    public static string AntiCheat => SafetyStrings.Safety_Consent_AntiCheat;

    public static string Terms => SafetyStrings.Safety_Consent_Terms;

    public static string Phrase => SafetyStrings.Safety_Consent_Phrase;

    public static string EnableText => SafetyStrings.Safety_Consent_Enable;

    public static string KeepOffText => SafetyStrings.Safety_Consent_KeepOff;

    /// <summary>The phrase, exactly (ordinal, trimmed of surrounding whitespace only): a typed acknowledgement is not a click.</summary>
    public bool IsAcknowledged => IsTheAcknowledgement(Typed);

    public static bool IsTheAcknowledgement(string? typed) => string.Equals(typed?.Trim(), SafetyStrings.Safety_Consent_Phrase, StringComparison.Ordinal);
}

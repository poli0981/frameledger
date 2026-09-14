using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FrameLedger.App.Services;
using FrameLedger.Application.TriState;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.App.ViewModels;

/// <summary>FR-8.3's dialog content: measured / Yes / No / N/A, and "also this game's default". The measured value stays stored either way.</summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture, which changes at runtime")]
public sealed partial class TriStateOverrideViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _useMeasured;

    [ObservableProperty]
    private bool _yes;

    [ObservableProperty]
    private bool _no;

    [ObservableProperty]
    private bool _notApplicable;

    [ObservableProperty]
    private bool _setAsDefault;

    public TriStateOverrideViewModel(TriStateChipModel chip)
    {
        ArgumentNullException.ThrowIfNull(chip);
        Title = string.Format(CultureInfo.CurrentCulture, Strings.Override_Title_Format, chip.Label);
        if (chip.Source != TriStateSource.Manual)
        {
            _useMeasured = true;
        }
        else
        {
            _yes = chip.Value == Tri.Yes;
            _no = chip.Value == Tri.No;
            _notApplicable = chip.Value == Tri.NotApplicable;
        }
    }

    public string Title { get; }

    public static string Body => Strings.Override_Body;

    public static string KeepText => Strings.Override_Keep;

    public static string YesText => Strings.Chip_Yes;

    public static string NoText => Strings.Chip_No;

    public static string NaText => Strings.Chip_NA;

    public static string DefaultText => Strings.Override_Default;

    public static string ApplyText => Strings.Override_Apply;

    public TriStateOverrideChoice Choice() => new(
        UseMeasured ? null : Yes ? Tri.Yes : No ? Tri.No : Tri.NotApplicable,
        SetAsDefault);
}

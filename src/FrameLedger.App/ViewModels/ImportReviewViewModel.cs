using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.Application.Import;

namespace FrameLedger.App.ViewModels;

/// <summary>FR-1.2's review checklist: every candidate the stores found, ticked by default where it can be imported; the count follows the ticks.</summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime; a cached CompositeFormat would pin the first culture")]
public sealed partial class ImportReviewViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    [NotifyPropertyChangedFor(nameof(SelectedText))]
    private int _selectedCount;

    public ImportReviewViewModel(IReadOnlyList<ImportCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        foreach (ImportCandidate c in candidates)
        {
            var row = new ImportCandidateViewModel(c);
            row.PropertyChanged += OnRowChanged;
            Rows.Add(row);
        }

        Recount();
    }

    public ObservableCollection<ImportCandidateViewModel> Rows { get; } = [];

    public static string Title => Strings.Import_Title;

    public static string Intro => Strings.Import_Intro;

    public static string ColumnPlatform => Strings.Import_Column_Platform;

    public static string ColumnName => Strings.Import_Column_Name;

    public static string ColumnExe => Strings.Import_Column_Exe;

    public static string ColumnNote => Strings.Import_Column_Note;

    public bool CanImport => SelectedCount > 0;

    public string SelectedText => string.Format(CultureInfo.CurrentCulture, Strings.Import_Selected_Format, SelectedCount, Rows.Count);

    public IReadOnlyList<ImportCandidate> Selected => [.. Rows.Where(static r => r.IsSelected && r.CanImport).Select(static r => r.Candidate)];

    [RelayCommand]
    private void SelectAll()
    {
        foreach (ImportCandidateViewModel r in Rows.Where(static r => r.CanImport))
        {
            r.IsSelected = true;
        }
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (ImportCandidateViewModel r in Rows)
        {
            r.IsSelected = false;
        }
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ImportCandidateViewModel.IsSelected), StringComparison.Ordinal))
        {
            Recount();
        }
    }

    private void Recount() => SelectedCount = Rows.Count(static r => r.IsSelected && r.CanImport);
}

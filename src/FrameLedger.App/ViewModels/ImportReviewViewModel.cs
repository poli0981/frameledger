// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.Application.Import;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// FR-1.2's review checklist: every candidate the stores found, ticked by default where it can be imported; the count
/// follows the ticks. Since beta.11 (owner request 2026-10-03) the games already in the library are hidden by default —
/// on a re-import they were most of the list and pushed the new ones out of sight — and <see cref="ShowExisting"/> brings
/// them back, dimmed, their boxes disabled.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime; a cached CompositeFormat would pin the first culture")]
public sealed partial class ImportReviewViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    [NotifyPropertyChangedFor(nameof(SelectedText))]
    private int _selectedCount;

    [ObservableProperty]
    private bool _showExisting;

    public ImportReviewViewModel(IReadOnlyList<ImportCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        foreach (ImportCandidate c in candidates)
        {
            var row = new ImportCandidateViewModel(c);
            row.PropertyChanged += OnRowChanged;
            Rows.Add(row);
        }

        ExistingCount = Rows.Count(static r => r.IsExisting);
        ShowExistingText = string.Format(CultureInfo.CurrentCulture, Strings.Import_ShowExisting_Format, ExistingCount);
        Refilter();
        Recount();
    }

    /// <summary>Every candidate, shown or not.</summary>
    public ObservableCollection<ImportCandidateViewModel> Rows { get; } = [];

    /// <summary>What the checklist shows: every row, less those already in the library unless <see cref="ShowExisting"/>.</summary>
    public ObservableCollection<ImportCandidateViewModel> Shown { get; } = [];

    /// <summary>Rows already in the library (at this path or another drive letter).</summary>
    public int ExistingCount { get; }

    public bool HasExisting => ExistingCount > 0;

    public string ShowExistingText { get; }

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

    partial void OnShowExistingChanged(bool value) => Refilter();

    private void Refilter()
    {
        Shown.Clear();
        foreach (ImportCandidateViewModel r in Rows.Where(r => ShowExisting || !r.IsExisting))
        {
            Shown.Add(r);
        }
    }
}

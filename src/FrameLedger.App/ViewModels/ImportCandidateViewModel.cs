using CommunityToolkit.Mvvm.ComponentModel;
using FrameLedger.App.Services;
using FrameLedger.Application.Import;

namespace FrameLedger.App.ViewModels;

/// <summary>One row of FR-1.2's review checklist: ticked by default when it can be imported, with the store, the name, the executable the import would add, and why it cannot when it cannot.</summary>
public sealed partial class ImportCandidateViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public ImportCandidateViewModel(ImportCandidate candidate)
    {
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        _isSelected = candidate.CanImport;
        Note = candidate.AlreadyInLibrary ? Strings.Import_Note_Already
            : candidate.ExePath is null || candidate.ExecutableMissing ? Strings.Import_Note_NoExe
            : candidate.ExecutableGuessed ? Strings.Import_Note_Guessed
            : string.Empty;
    }

    public ImportCandidate Candidate { get; }

    public bool CanImport => Candidate.CanImport;

    public string PlatformText => Formats.Platform(Candidate.Game.Platform);

    public string Name => Candidate.Game.Name;

    public string ExeText => Candidate.ExePath ?? Strings.Common_Dash;

    /// <summary>Already in the library, no executable found, or a guessed executable the user should check.</summary>
    public string Note { get; }
}

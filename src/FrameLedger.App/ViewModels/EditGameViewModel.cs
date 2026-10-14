// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Text.Json;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// FR-1.3: the four fields a user edits most (name, publisher, version, notes) and, since beta.23 (D60), the game's image;
/// the rest of <see cref="GameMetadata"/> passes through unchanged. A field detection filled in (<c>field_provenance</c> =
/// <c>detected</c>) is badged so the user knows what they are overriding.
/// </summary>
/// <remarks>
/// A picked image is only previewed here: it is copied into the data folder once the dialog closed with Save
/// (<see cref="GameEdit.PickedImage"/>), so Cancel leaves nothing behind.
/// </remarks>
public sealed partial class EditGameViewModel : ObservableObject
{
    private readonly GameMetadata _original;
    private readonly EditImage? _image;

    // The user went back to the store's image: the row's own image is dropped on Save.
    private bool _dropOwn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImagePreview))]
    private ImageSource? _imagePreview;

    /// <summary>The image file picked in this dialog, copied on Save; null when none was picked.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseStoreImage))]
    private string? _pickedImage;

    /// <summary>The last pick could not be read as an image (shown under the buttons); null otherwise.</summary>
    [ObservableProperty]
    private string? _imageError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseStoreImage))]
    private bool _showsOwnImage;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _publisher;

    [ObservableProperty]
    private string _version;

    [ObservableProperty]
    private string _notes;

    public EditGameViewModel(GameMetadata current, string? provenanceJson, EditImage? image = null)
    {
        _original = current ?? throw new ArgumentNullException(nameof(current));
        _image = image;
        _imagePreview = image?.Current;
        _showsOwnImage = image is { CurrentIsOwn: true };
        _name = current.Name;
        _publisher = current.Publisher ?? string.Empty;
        _version = current.GameVersion ?? string.Empty;
        _notes = current.Notes ?? string.Empty;
        Dictionary<string, string> provenance = ParseProvenance(provenanceJson);
        NameDetected = IsDetected(provenance, "name");
        PublisherDetected = IsDetected(provenance, "publisher");
        VersionDetected = IsDetected(provenance, "game_version");
    }

    public bool NameDetected { get; }

    public bool PublisherDetected { get; }

    public bool VersionDetected { get; }

    public static string Title => Strings.EditGame_Title;

    public static string NameLabel => Strings.EditGame_Name;

    public static string PublisherLabel => Strings.EditGame_Publisher;

    public static string VersionLabel => Strings.EditGame_Version;

    public static string NotesLabel => Strings.EditGame_Notes;

    public static string DetectedBadge => Strings.EditGame_Detected;

    public static string ImageLabel => Strings.EditGame_Image;

    public static string ChooseImageText => Strings.EditGame_ChooseImage;

    public static string UseStoreImageText => Strings.EditGame_UseStoreImage;

    public static string ImageNoneText => Strings.EditGame_ImageNone;

    public static string ImageNote => Strings.EditGame_ImageNote;

    public static string CoverName => Strings.Art_Cover_Name;

    /// <summary>Whether the dialog has an image row: the game page passed what it shows.</summary>
    public bool ImageEditable => _image is not null;

    public bool HasImagePreview => ImagePreview is not null;

    /// <summary>The image shown is the user's own (kept or just picked), so going back to the store's means something.</summary>
    public bool CanUseStoreImage => PickedImage is not null || ShowsOwnImage;

    public bool CanSave => !string.IsNullOrWhiteSpace(Name);

    /// <summary>
    /// The metadata to store: the four fields as typed (blank → null), the row's own image dropped when the user went back to
    /// the store's, everything else as it was.
    /// </summary>
    public GameMetadata Result() => _original with
    {
        Name = Name.Trim(),
        Publisher = Blank(Publisher),
        GameVersion = Blank(Version),
        Notes = Blank(Notes),
        CoverPath = _dropOwn ? null : _original.CoverPath,
    };

    /// <summary>What Save answers: <see cref="Result"/> and the image picked in this dialog.</summary>
    public GameEdit Edit() => new(Result(), PickedImage);

    /// <summary>"Choose image…": the picked file previewed; it is copied only on Save, and one that cannot be read is said here.</summary>
    [RelayCommand]
    private async Task ChooseImageAsync()
    {
        if (_image?.Picker.PickImage() is not { } path)
        {
            return;
        }

        if (await _image.Preview(path).ConfigureAwait(true) is { } preview)
        {
            PickedImage = path;
            ImagePreview = preview;
            ImageError = null;
        }
        else
        {
            ImageError = Strings.Art_ImportFailed;
        }
    }

    /// <summary>"Use the store's image": the pick and the row's own image dropped; the page shows the store's again.</summary>
    [RelayCommand]
    private void UseStoreImage()
    {
        PickedImage = null;
        _dropOwn = _original.CoverPath is not null;
        ShowsOwnImage = false;
        ImagePreview = _image?.Store;
        ImageError = null;
    }

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(CanSave));

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Whether <paramref name="field"/> of <c>field_provenance</c> is badged <c>detected</c> (a store or the detection sweep wrote it).</summary>
    internal static bool IsDetectedField(string? provenanceJson, string field) => IsDetected(ParseProvenance(provenanceJson), field);

    private static bool IsDetected(Dictionary<string, string> provenance, string field) =>
        provenance.TryGetValue(field, out string? p) && string.Equals(p, "detected", StringComparison.Ordinal);

    private static Dictionary<string, string> ParseProvenance(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize(json, AppJsonContext.Default.DictionaryStringString) ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}

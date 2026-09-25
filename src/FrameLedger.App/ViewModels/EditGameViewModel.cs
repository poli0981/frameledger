using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// FR-1.3: the four fields a user edits most (name, publisher, version, notes); the rest of <see cref="GameMetadata"/>
/// passes through unchanged. A field detection filled in (<c>field_provenance</c> = <c>detected</c>) is badged so the
/// user knows what they are overriding.
/// </summary>
public sealed partial class EditGameViewModel : ObservableObject
{
    private readonly GameMetadata _original;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _publisher;

    [ObservableProperty]
    private string _version;

    [ObservableProperty]
    private string _notes;

    public EditGameViewModel(GameMetadata current, string? provenanceJson)
    {
        _original = current ?? throw new ArgumentNullException(nameof(current));
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

    public bool CanSave => !string.IsNullOrWhiteSpace(Name);

    /// <summary>The metadata to store: the four fields as typed (blank → null), everything else as it was.</summary>
    public GameMetadata Result() => _original with
    {
        Name = Name.Trim(),
        Publisher = Blank(Publisher),
        GameVersion = Blank(Version),
        Notes = Blank(Notes),
    };

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

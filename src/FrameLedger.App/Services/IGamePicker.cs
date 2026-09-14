namespace FrameLedger.App.Services;

/// <summary>FR-1.1's file pick, behind an interface so a test can answer it without a dialog.</summary>
public interface IGamePicker
{
    /// <summary>The chosen executable's full path, or null when the user cancelled.</summary>
    string? PickExecutable();
}

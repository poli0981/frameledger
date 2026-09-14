using Microsoft.Win32;

namespace FrameLedger.App.Services;

/// <summary>The WPF <see cref="OpenFileDialog"/> for FR-1.1.</summary>
public sealed class GamePicker : IGamePicker
{
    public string? PickExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.AddGame_Dialog_Title,
            Filter = Strings.AddGame_Filter,
            CheckFileExists = true,
            Multiselect = false,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}

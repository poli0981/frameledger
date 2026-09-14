using Microsoft.Win32;

namespace FrameLedger.App.Services;

/// <summary>The WPF <see cref="SaveFileDialog"/>.</summary>
public sealed class FileSaver : IFileSaver
{
    public string? PickSavePath(string filter, string suggestedName)
    {
        var dialog = new SaveFileDialog { Filter = filter, FileName = suggestedName, AddExtension = true, OverwritePrompt = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}

using System.Runtime.InteropServices;

namespace FrameLedger.App.Services;

/// <summary><see cref="IClipboard"/> over WPF's; the clipboard can be held by another process, which is a refusal, never a crash.</summary>
public sealed class WpfClipboard : IClipboard
{
    public bool SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            System.Windows.Clipboard.SetText(text);
            return true;
        }
        catch (ExternalException ex)
        {
            Serilog.Log.Warning(ex, "ui: the clipboard refused the text");
            return false;
        }
    }
}

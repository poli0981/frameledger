using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

internal sealed class NoClipboard(bool accepts = true) : IClipboard
{
    public List<string> Texts { get; } = [];

    public bool SetText(string text)
    {
        Texts.Add(text);
        return accepts;
    }
}

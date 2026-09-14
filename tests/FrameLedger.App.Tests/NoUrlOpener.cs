using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

internal sealed class NoUrlOpener(bool accepts = true) : IUrlOpener
{
    public List<Uri> Opened { get; } = [];

    public bool Open(Uri url)
    {
        Opened.Add(url);
        return accepts;
    }
}

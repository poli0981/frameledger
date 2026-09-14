using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>An <see cref="IMessageStrip"/> that remembers what it was told.</summary>
internal sealed class RecordingStrip : IMessageStrip
{
    public List<(string Kind, string Title, string Body)> Shown { get; } = [];

    public void Info(string title, string body) => Shown.Add(("info", title, body));

    public void Success(string title, string body) => Shown.Add(("success", title, body));

    public void Warn(string title, string body) => Shown.Add(("warn", title, body));
}

namespace FrameLedger.App.Services;

/// <summary>A tray balloon the view model asks for (FR-3.8): title, body, and the session its click opens.</summary>
public sealed class TrayToastEventArgs(string title, string body, long? sessionId) : EventArgs
{
    public string Title { get; } = title ?? throw new ArgumentNullException(nameof(title));

    public string Body { get; } = body ?? throw new ArgumentNullException(nameof(body));

    public long? SessionId { get; } = sessionId;
}

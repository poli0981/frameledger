namespace FrameLedger.App.Services;

/// <summary>The clipboard (P4 PR-3, the bug report's Markdown fallback) — a port so the flow is testable without a desktop.</summary>
public interface IClipboard
{
    /// <summary>True when the text was placed; false when the clipboard refused (another process holding it), logged by the adapter.</summary>
    bool SetText(string text);
}

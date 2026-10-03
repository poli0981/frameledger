// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using FrameLedger.App.Markdown;
using FrameLedger.App.Services;

namespace FrameLedger.App.Controls;

/// <summary>
/// A document the App shows, rendered (beta.12, owner request 4): Markdown through <see cref="MarkdownRenderer"/>, or a plain
/// text kept exactly as written (<see cref="IsPlainText"/> — the GPL's <c>LICENSE</c>). Read-only, selectable, copyable
/// (Ctrl+C), zoomable (Ctrl+wheel). Links follow <see cref="MarkdownLinks"/>: http, https and mailto through
/// <see cref="UrlOpener"/>; <c>#section</c> scrolls; a file of the repository is offered to the host first
/// (<see cref="RepositoryLinkRequested"/>), which shows it when the App embeds it, and otherwise opens on GitHub at this
/// build's source (<see cref="RepositoryLinks"/>); anything else is not followed.
/// </summary>
public sealed class MarkdownView : FlowDocumentScrollViewer
{
    public static readonly DependencyProperty MarkdownTextProperty =
        DependencyProperty.Register(nameof(MarkdownText), typeof(string), typeof(MarkdownView), new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty IsPlainTextProperty =
        DependencyProperty.Register(nameof(IsPlainText), typeof(bool), typeof(MarkdownView), new PropertyMetadata(false, OnSourceChanged));

    public static readonly DependencyProperty DocumentPathProperty =
        DependencyProperty.Register(nameof(DocumentPath), typeof(string), typeof(MarkdownView), new PropertyMetadata(null, OnSourceChanged));

    private IReadOnlyDictionary<string, Block> _anchors = new Dictionary<string, Block>(StringComparer.Ordinal);

    public MarkdownView()
    {
        IsToolBarVisible = false;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Focusable = true;
        AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnRequestNavigate));
        Rebuild();
    }

    /// <summary>A link to another file of the repository; the host shows it and sets Handled, or it opens on GitHub.</summary>
    public event EventHandler<RepositoryLinkEventArgs>? RepositoryLinkRequested;

    /// <summary>The document's text.</summary>
    public string? MarkdownText
    {
        get => (string?)GetValue(MarkdownTextProperty);
        set => SetValue(MarkdownTextProperty, value);
    }

    /// <summary>Show the text as written — monospace, every line kept — rather than as Markdown.</summary>
    public bool IsPlainText
    {
        get => (bool)GetValue(IsPlainTextProperty);
        set => SetValue(IsPlainTextProperty, value);
    }

    /// <summary>The document's path from the repository root, which its relative links are resolved against.</summary>
    public string? DocumentPath
    {
        get => (string?)GetValue(DocumentPathProperty);
        set => SetValue(DocumentPathProperty, value);
    }

    /// <summary>How a link leaves the App; the shell's opener unless the host gives its own.</summary>
    public IUrlOpener UrlOpener { get; set; } = new ShellUrlOpener();

    /// <summary>The headings this document can scroll to, by anchor.</summary>
    public IReadOnlyCollection<string> Anchors => [.. _anchors.Keys];

    /// <summary>Brings the heading with <paramref name="anchor"/> into view; whether the document has it.</summary>
    public bool ScrollTo(string anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        if (!_anchors.TryGetValue(anchor, out Block? block))
        {
            return false;
        }

        block.BringIntoView();
        return true;
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MarkdownView)d).Rebuild();

    private void Rebuild()
    {
        string text = MarkdownText ?? string.Empty;
        RenderedMarkdown rendered = IsPlainText ? MarkdownRenderer.RenderPlainText(text) : MarkdownRenderer.Render(text, DocumentPath);
        _anchors = rendered.Anchors;
        Document = rendered.Document;
    }

    private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        MarkdownLinkTarget target = MarkdownLinks.Resolve(DocumentPath, e.Uri?.OriginalString);
        if (target.External is { } external)
        {
            _ = UrlOpener.Open(external);
        }
        else if (target.RepositoryPath is { } path)
        {
            var args = new RepositoryLinkEventArgs(path, target.Anchor);
            RepositoryLinkRequested?.Invoke(this, args);
            if (!args.Handled)
            {
                _ = UrlOpener.Open(RepositoryLinks.Blob(path, target.Anchor));
            }
        }
        else if (target.Anchor is { } anchor)
        {
            _ = ScrollTo(anchor);
        }
    }
}

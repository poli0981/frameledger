// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
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
/// <remarks>
/// <para>
/// <b>The wheel (beta.21, owner request 2026-10-13: "scrolling the documents is slow").</b> WPF's scroll viewer moves a
/// fixed 48 DIP per wheel message, whatever its delta — about two and a half lines of body text, Windows' "lines to
/// scroll" ignored, and a precision touchpad's small deltas each jumping the whole step (measured: −1, −15, −120 and −360
/// all moved 48). Layout costs about a millisecond a step, so the step was the problem, not drawing. The view now moves
/// <see cref="WheelStep"/>: the delta in proportion, Windows' lines per notch at a browser's line (100/3 DIP), scaled by
/// the zoom — and eases there over about 120 ms unless Windows' animations are off. At either end it leaves the wheel
/// unhandled, so whatever holds the view (the update notes' dialog) scrolls instead. Ctrl+wheel stays the zoom.
/// </para>
/// <para>
/// <b>One render per page.</b> Each of the three properties rendered the whole document on its own, so a page change
/// rendered the old text under the new path first, and the Legal Gate's three bindings rendered three times. Property
/// changes now ask for one render, posted to the dispatcher ahead of layout; <see cref="Show"/> sets all three and renders
/// at once; and a document this view has rendered is kept (<see cref="CacheCapacity"/>), so going back to a page costs
/// its layout and nothing else.
/// </para>
/// </remarks>
public sealed class MarkdownView : FlowDocumentScrollViewer
{
    /// <summary>A line of the wheel's step: Chromium scrolls 100 DIP for Windows' default three lines at 100%.</summary>
    internal const double WheelLineDip = 100.0 / 3.0;

    /// <summary>How many rendered documents one view keeps (the user guide has twelve pages and Limitations).</summary>
    internal const int CacheCapacity = 16;

    public static readonly DependencyProperty MarkdownTextProperty =
        DependencyProperty.Register(nameof(MarkdownText), typeof(string), typeof(MarkdownView), new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty IsPlainTextProperty =
        DependencyProperty.Register(nameof(IsPlainText), typeof(bool), typeof(MarkdownView), new PropertyMetadata(false, OnSourceChanged));

    public static readonly DependencyProperty DocumentPathProperty =
        DependencyProperty.Register(nameof(DocumentPath), typeof(string), typeof(MarkdownView), new PropertyMetadata(null, OnSourceChanged));

    private readonly Dictionary<RenderKey, RenderedMarkdown> _cache = [];
    private readonly LinkedList<RenderKey> _recent = new();
    private IReadOnlyDictionary<string, Block> _anchors = new Dictionary<string, Block>(StringComparer.Ordinal);
    private RenderKey? _shown;
    private bool _batching;
    private DispatcherOperation? _pending;

    private ScrollViewer? _scroller;
    private double _wheelTarget = double.NaN;
    private double _lastSetOffset = double.NaN;
    private TimeSpan _lastFrame;
    private bool _easing;

    public MarkdownView()
    {
        IsToolBarVisible = false;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Focusable = true;
        AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnRequestNavigate));
        Unloaded += (_, _) => StopEasing();
        RenderNow();
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
    public IReadOnlyCollection<string> Anchors
    {
        get
        {
            RenderIfPending();
            return [.. _anchors.Keys];
        }
    }

    /// <summary>How many documents this view has rendered from text — a cache hit is not one (for tests).</summary>
    internal int Renders { get; private set; }

    /// <summary>Where the wheel is taking the view, or NaN when it is not easing it there (for tests).</summary>
    internal double ScrollTarget => _wheelTarget;

    /// <summary>The template's scroll viewer, once the template is applied (for tests).</summary>
    internal ScrollViewer? Scroller => _scroller;

    /// <summary>The modifier keys held, read when the wheel turns; a test gives its own rather than the real keyboard's.</summary>
    internal Func<ModifierKeys> CurrentModifiers { get; set; } = static () => Keyboard.Modifiers;

    /// <summary>Windows' "lines to scroll" per notch (−1: a screen); a test gives its own.</summary>
    internal Func<int> WheelScrollLines { get; set; } = static () => SystemParameters.WheelScrollLines;

    /// <summary>Whether the wheel eases to its target; off where Windows' animations are, and in tests.</summary>
    internal bool EaseWheel { get; set; } = SystemParameters.ClientAreaAnimation;

    /// <summary>Shows a document: the three properties at once, rendered once, now.</summary>
    public void Show(string? text, bool isPlainText, string? path)
    {
        _batching = true;
        try
        {
            DocumentPath = path;
            IsPlainText = isPlainText;
            MarkdownText = text;
        }
        finally
        {
            _batching = false;
        }

        RenderNow();
    }

    /// <summary>Brings the heading with <paramref name="anchor"/> into view; whether the document has it.</summary>
    public bool ScrollTo(string anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        RenderIfPending();
        if (!_anchors.TryGetValue(anchor, out Block? block))
        {
            return false;
        }

        StopEasing();
        block.BringIntoView();
        return true;
    }

    /// <summary>
    /// The distance one wheel message moves the view, in DIP (positive: down). <paramref name="delta"/> in proportion (120 is a
    /// notch); <paramref name="wheelScrollLines"/> lines per notch at <see cref="WheelLineDip"/>, or a screen
    /// (<paramref name="viewportHeight"/>) when Windows says −1; scaled by <paramref name="zoomPercent"/>.
    /// </summary>
    internal static double WheelStep(int delta, int wheelScrollLines, double viewportHeight, double zoomPercent)
    {
        double notches = delta / 120.0;
        double perNotch = wheelScrollLines < 0 ? viewportHeight : wheelScrollLines * WheelLineDip * zoomPercent / 100.0;
        return -notches * perNotch;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        StopEasing();
        _scroller = GetTemplateChild("PART_ContentHost") as ScrollViewer;
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPreviewMouseWheel(e);
        if (e.Handled || _scroller is not { } scroller || (CurrentModifiers() & ModifierKeys.Control) != ModifierKeys.None)
        {
            return;    // Ctrl+wheel is the zoom
        }

        double step = WheelStep(e.Delta, WheelScrollLines(), scroller.ViewportHeight, Zoom);
        if (step == 0 || double.IsNaN(step))
        {
            return;
        }

        double from = double.IsNaN(_wheelTarget) ? scroller.VerticalOffset : _wheelTarget;
        double to = Math.Clamp(from + step, 0, scroller.ScrollableHeight);
        if (Math.Abs(to - scroller.VerticalOffset) < 0.5 && double.IsNaN(_wheelTarget))
        {
            return;    // at the end it would move toward: the wheel is the host's
        }

        e.Handled = true;
        if (!EaseWheel)
        {
            scroller.ScrollToVerticalOffset(to);
            return;
        }

        _wheelTarget = to;
        if (!_easing)
        {
            _easing = true;
            _lastFrame = TimeSpan.Zero;
            _lastSetOffset = scroller.VerticalOffset;
            CompositionTarget.Rendering += OnRendering;
        }
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MarkdownView)d).RequestRender();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "a WPF application has no JoinableTaskFactory; this posts to the view's own dispatcher from its own thread, at a priority ahead of layout, and never waits (ScrollBarHiding does the same)")]
    private void RequestRender()
    {
        if (_batching || _pending is not null)
        {
            return;
        }

        // Ahead of layout, after whatever is setting the other properties has set them.
        _pending = Dispatcher.InvokeAsync(RenderIfPending, DispatcherPriority.DataBind);
    }

    private void RenderIfPending()
    {
        if (_pending is null)
        {
            return;
        }

        RenderNow();
    }

    private void RenderNow()
    {
        if (_pending is { } pending)
        {
            _ = pending.Abort();
            _pending = null;
        }

        var key = new RenderKey(DocumentPath, IsPlainText, MarkdownText ?? string.Empty);
        if (_shown is { } shown && shown.Equals(key))
        {
            return;
        }

        if (!_cache.TryGetValue(key, out RenderedMarkdown? rendered))
        {
            rendered = key.IsPlainText ? MarkdownRenderer.RenderPlainText(key.Text) : MarkdownRenderer.Render(key.Text, key.Path);
            Renders++;
            _cache[key] = rendered;
        }
        else
        {
            _ = _recent.Remove(key);
        }

        _ = _recent.AddFirst(key);
        while (_recent.Count > CacheCapacity && _recent.Last is { } oldest)
        {
            _ = _cache.Remove(oldest.Value);
            _recent.RemoveLast();
        }

        StopEasing();
        _anchors = rendered.Anchors;
        _shown = key;
        Document = rendered.Document;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_scroller is not { } scroller || double.IsNaN(_wheelTarget))
        {
            StopEasing();
            return;
        }

        // Something else moved the view since the last frame (the scroll bar, the keyboard, a link): it is theirs now.
        if (Math.Abs(scroller.VerticalOffset - _lastSetOffset) > 1)
        {
            StopEasing();
            return;
        }

        TimeSpan now = (e as RenderingEventArgs)?.RenderingTime ?? TimeSpan.Zero;
        double elapsedMs = _lastFrame == TimeSpan.Zero ? 16 : Math.Clamp((now - _lastFrame).TotalMilliseconds, 1, 100);
        _lastFrame = now;

        // Exponential ease-out: about 95% of the way in 120 ms, however many notches have been added on the way.
        double remaining = _wheelTarget - scroller.VerticalOffset;
        double next = Math.Abs(remaining) < 0.5 ? _wheelTarget : scroller.VerticalOffset + (remaining * (1 - Math.Exp(-elapsedMs / 40.0)));
        scroller.ScrollToVerticalOffset(next);
        _lastSetOffset = next;
        if (next == _wheelTarget)
        {
            StopEasing();
        }
    }

    private void StopEasing()
    {
        _wheelTarget = double.NaN;
        if (_easing)
        {
            _easing = false;
            CompositionTarget.Rendering -= OnRendering;
        }
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

    /// <summary>What a rendered document depends on: theme and language are not in it — its colours are resource references.</summary>
    private readonly record struct RenderKey(string? Path, bool IsPlainText, string Text);
}

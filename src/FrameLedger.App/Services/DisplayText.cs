// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Display;

namespace FrameLedger.App.Services;

/// <summary>
/// A session's display facts as the pages say them (beta.10, <c>03_METRICS</c> §Display mode): the mode that lasted longest
/// with its share, every mode's share, and the sizes and the source behind them. A mode is never named more certainly than
/// its source allows — a window that covered its monitor while nothing could ask the swap chain is "Fullscreen or
/// borderless", never either.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public static class DisplayText
{
    /// <summary>The order the shares are said in: the fullscreen end first.</summary>
    private static readonly DisplayMode[] _order =
        [DisplayMode.ExclusiveFullscreen, DisplayMode.Borderless, DisplayMode.CoversScreen, DisplayMode.Windowed, DisplayMode.Minimized];

    /// <summary>The row's display facts, or null for a row that has none (written before beta.10, or recovered from a crash file).</summary>
    public static DisplaySummary? Of(SessionRow row) => SessionDisplayColumns.DisplayOf(row);

    public static string ModeName(DisplayMode mode) => mode switch
    {
        DisplayMode.ExclusiveFullscreen => Strings.Display_Mode_Exclusive,
        DisplayMode.Borderless => Strings.Display_Mode_Borderless,
        DisplayMode.CoversScreen => Strings.Display_Mode_CoversScreen,
        DisplayMode.Windowed => Strings.Display_Mode_Windowed,
        DisplayMode.Minimized => Strings.Display_Mode_Minimized,
        _ => Strings.Display_Mode_NoWindow,
    };

    /// <summary>
    /// The mode that lasted longest and its share (<c>Borderless 92%</c>); "No window" when a window was never read; N/A for a
    /// row without display facts.
    /// </summary>
    public static string Headline(DisplaySummary? display)
    {
        if (display is null)
        {
            return Strings.Common_NotAvailable;
        }

        return display.Dominant is { } mode
            ? Share(mode, display.SharePercent(mode))
            : display.MinimizedMs > 0 ? Share(DisplayMode.Minimized, display.SharePercent(DisplayMode.Minimized)) : Strings.Display_Mode_NoWindow;
    }

    /// <summary>Every mode the session spent time in, fullscreen end first (<c>Exclusive fullscreen 8% · Borderless 92%</c>).</summary>
    public static string Shares(DisplaySummary? display)
    {
        if (display is null)
        {
            return Strings.Common_NotAvailable;
        }

        string[] parts = [.. _order.Where(m => display.SharePercent(m) is > 0).Select(m => Share(m, display.SharePercent(m)))];
        return parts.Length == 0 ? Strings.Display_Mode_NoWindow : string.Join(" · ", parts);
    }

    /// <summary>The window's client size in the mode that lasted longest, or N/A.</summary>
    public static string Window(DisplaySummary? display) =>
        display is { WindowWidth: > 0 and int w, WindowHeight: > 0 and int h } ? Size(w, h) : Strings.Common_NotAvailable;

    /// <summary>The monitor the window was on, with its refresh rate when known, or N/A.</summary>
    public static string Monitor(DisplaySummary? display) => display switch
    {
        { MonitorWidth: > 0 and int w, MonitorHeight: > 0 and int h, MonitorHz: > 0 and int hz }
            => string.Format(CultureInfo.CurrentCulture, Strings.Display_SizeHz_Format, w, h, hz),
        { MonitorWidth: > 0 and int w, MonitorHeight: > 0 and int h } => Size(w, h),
        _ => Strings.Common_NotAvailable,
    };

    /// <summary>
    /// Everything the row says about the display, as one line for the session summary and the tooltips: the shares, the
    /// changes, the window on its monitor, the back buffer and its swap effect, and what it all rested on. Null for a row
    /// without display facts.
    /// </summary>
    public static string? Line(DisplaySummary? display)
    {
        if (display is null)
        {
            return null;
        }

        var parts = new List<string> { Shares(display) };
        if (display.Changes > 0)
        {
            parts.Add(display.Changes == 1
                ? Strings.Display_Changes_One
                : string.Format(CultureInfo.CurrentCulture, Strings.Display_Changes_Format, display.Changes));
        }

        if (display is { WindowWidth: > 0 and int ww, WindowHeight: > 0 and int wh })
        {
            parts.Add(display is { MonitorWidth: > 0 and int mw, MonitorHeight: > 0 and int mh }
                ? display.MonitorHz is > 0 and int hz
                    ? string.Format(CultureInfo.CurrentCulture, Strings.Display_WindowOnMonitorHz_Format, ww, wh, mw, mh, hz)
                    : string.Format(CultureInfo.CurrentCulture, Strings.Display_WindowOnMonitor_Format, ww, wh, mw, mh)
                : string.Format(CultureInfo.CurrentCulture, Strings.Display_Window_Format, ww, wh));
        }

        if (display is { BufferWidth: > 0 and uint bw, BufferHeight: > 0 and uint bh })
        {
            parts.Add(display.SwapEffect is { Length: > 0 } effect
                ? string.Format(CultureInfo.CurrentCulture, Strings.Display_BufferEffect_Format, bw, bh, effect)
                : string.Format(CultureInfo.CurrentCulture, Strings.Display_Buffer_Format, bw, bh));
        }

        if (SourceText(display.Source) is { } source)
        {
            parts.Add(source);
        }

        return string.Format(CultureInfo.CurrentCulture, Strings.Display_Line_Format, string.Join(" · ", parts));
    }

    /// <summary>What the facts rested on, said with what it means for exclusive fullscreen; null when no window was read.</summary>
    public static string? SourceText(string? source) => source switch
    {
        DisplaySource.SwapChain => Strings.Display_Source_SwapChain,
        DisplaySource.OpenGl => Strings.Display_Source_OpenGl,
        DisplaySource.Window => Strings.Display_Source_Window,
        _ => null,
    };

    /// <summary>
    /// The row's display facts for the CSV's <c># display:</c> line — InvariantCulture and the stored names, as every export
    /// line is, so a file reads the same in every UI language.
    /// </summary>
    public static string ExportLine(DisplaySummary? display)
    {
        if (display is null)
        {
            return "N/A (not recorded: a session recorded before beta.10, or recovered from its crash file)";
        }

        CultureInfo inv = CultureInfo.InvariantCulture;
        string Pair(int? w, int? h) => w is > 0 && h is > 0 ? w.Value.ToString(inv) + "x" + h.Value.ToString(inv) : "n/a";
        string buffer = display is { BufferWidth: > 0 and uint bw, BufferHeight: > 0 and uint bh } ? bw.ToString(inv) + "x" + bh.ToString(inv) : "n/a";
        string hz = display.MonitorHz is > 0 and int r ? "@" + r.ToString(inv) : string.Empty;
        return string.Join("; ",
            "exclusive_ms=" + display.ExclusiveMs.ToString(inv),
            "borderless_ms=" + display.BorderlessMs.ToString(inv),
            "covers_ms=" + display.CoversMs.ToString(inv),
            "windowed_ms=" + display.WindowedMs.ToString(inv),
            "minimized_ms=" + display.MinimizedMs.ToString(inv),
            "nowindow_ms=" + display.NoWindowMs.ToString(inv),
            "changes=" + display.Changes.ToString(inv),
            "source=" + (display.Source ?? "n/a"),
            "window=" + Pair(display.WindowWidth, display.WindowHeight),
            "buffer=" + buffer,
            "monitor=" + Pair(display.MonitorWidth, display.MonitorHeight) + hz);
    }

    /// <summary>A share as a person reads it: whole percent, and "&lt;1%" for time that was there but rounds to none.</summary>
    internal static string Percent(double share) => share is > 0 and < 0.5
        ? "<1%"
        : Math.Round(share, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.CurrentCulture) + "%";

    private static string Share(DisplayMode mode, double? share) =>
        share is double s ? string.Format(CultureInfo.CurrentCulture, Strings.Display_Share_Format, ModeName(mode), Percent(s)) : ModeName(mode);

    private static string Size(int w, int h) => string.Format(CultureInfo.CurrentCulture, Strings.Display_Size_Format, w, h);
}

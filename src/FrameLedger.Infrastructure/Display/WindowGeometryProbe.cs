// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FrameLedger.Application.Capture;
using FrameLedger.Domain.Display;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace FrameLedger.Infrastructure.Display;

/// <summary>
/// The game's window, read OUT of its process (beta.10, <c>03_METRICS</c> §Display mode): whether it is minimised, its
/// client area in screen coordinates, the monitor it is on and that monitor's refresh rate. Documented user32 questions
/// about a top-level window — the focus sample's class of call (<c>Io.ForegroundWindowProbe</c>) — and nothing belonging to
/// the game: no handle to its process, no memory (CLAUDE.md rule 4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Per-monitor DPI aware while it asks.</b> The Agent has no manifest and runs DPI-unaware, and Windows virtualises the
/// coordinates a DPI-unaware process reads of another process's window — a borderless game on a 150 % monitor would read as
/// a window two-thirds its size. The thread is made per-monitor aware (V2) for the calls and put back, so nothing else in the
/// Agent changes.
/// </para>
/// <para>
/// <b>Which window.</b> The one the swap chain presents to when the injected component named it and it still belongs to the
/// game (a handle is reused once its window is gone), taken to its top-level ancestor — an engine may render into a child.
/// Otherwise the largest visible, uncloaked, non-tool top-level window of any of the game's processes: a Chromium-based
/// title's window belongs to its browser process while another process presents (HANDOFF §Traps), and every process
/// running the game's executable counts.
/// </para>
/// </remarks>
public sealed class WindowGeometryProbe : IDisplayProbe
{
    /// <summary><c>DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2</c>, which winuser.h defines as <c>((DPI_AWARENESS_CONTEXT)-4)</c>.</summary>
    private static readonly DPI_AWARENESS_CONTEXT _perMonitorAwareV2 = new(-4);

    /// <inheritdoc />
    public unsafe WindowView? Observe(IReadOnlyCollection<int> pids, ulong hwnd)
    {
        ArgumentNullException.ThrowIfNull(pids);
        if (pids.Count == 0)
        {
            return null;
        }

        DPI_AWARENESS_CONTEXT previous = PInvoke.SetThreadDpiAwarenessContext(_perMonitorAwareV2);
        try
        {
            HWND window = hwnd != 0 ? Root(new HWND((nint)hwnd), pids) : default;
            if (window.IsNull)
            {
                window = Largest(pids);
            }

            return window.IsNull ? null : Read(window);
        }
        finally
        {
            if (previous != default)
            {
                PInvoke.SetThreadDpiAwarenessContext(previous);
            }
        }
    }

    /// <summary>The top-level ancestor of <paramref name="window"/> when it is a live window of one of <paramref name="pids"/>; else null.</summary>
    private static HWND Root(HWND window, IReadOnlyCollection<int> pids)
    {
        if (!PInvoke.IsWindow(window))
        {
            return default;
        }

        HWND root = PInvoke.GetAncestor(window, GET_ANCESTOR_FLAGS.GA_ROOT);
        HWND top = root.IsNull ? window : root;
        return OwnerIn(top, pids) ? top : default;
    }

    private static unsafe bool OwnerIn(HWND window, IReadOnlyCollection<int> pids)
    {
        uint owner = 0;
        return PInvoke.GetWindowThreadProcessId(window, &owner) != 0 && pids.Contains((int)owner);
    }

    private static unsafe HWND Largest(IReadOnlyCollection<int> pids)
    {
        var search = new Search(pids);
        GCHandle handle = GCHandle.Alloc(search);
        try
        {
            PInvoke.EnumWindows(&Visit, (LPARAM)GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        return search.Best;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe BOOL Visit(HWND window, LPARAM state)
    {
        var search = (Search)GCHandle.FromIntPtr(state).Target!;
        if (!PInvoke.IsWindowVisible(window) || !OwnerIn(window, search.Pids) || IsToolWindow(window) || IsCloaked(window))
        {
            return true;
        }

        RECT client;
        if (PInvoke.GetClientRect(window, &client))
        {
            long area = (long)(client.right - client.left) * (client.bottom - client.top);
            if (area > search.BestArea || (search.Best.IsNull && PInvoke.IsIconic(window)))
            {
                search.Best = window;
                search.BestArea = area;
            }
        }

        return true;
    }

    private static bool IsToolWindow(HWND window) =>
        ((WINDOW_EX_STYLE)(nuint)PInvoke.GetWindowLongPtr(window, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE)).HasFlag(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW);

    private static unsafe bool IsCloaked(HWND window)
    {
        // Read through a pointer the call writes: DWMWA_CLOAKED is non-zero for a window another virtual desktop or the
        // shell hides, which is visible to IsWindowVisible and to nobody else.
        uint* cloaked = stackalloc uint[1];
        return PInvoke.DwmGetWindowAttribute(window, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, cloaked, sizeof(uint)).Succeeded && *cloaked != 0;
    }

    private static unsafe WindowView? Read(HWND window)
    {
        bool minimized = PInvoke.IsIconic(window);
        RECT client;
        if (!PInvoke.GetClientRect(window, &client))
        {
            return null;
        }

        var topLeft = new System.Drawing.Point(client.left, client.top);
        var bottomRight = new System.Drawing.Point(client.right, client.bottom);
        if (!PInvoke.ClientToScreen(window, &topLeft) || !PInvoke.ClientToScreen(window, &bottomRight))
        {
            return null;
        }

        HMONITOR monitor = PInvoke.MonitorFromWindow(window, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFOEXW();
        info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);
        if (monitor.IsNull || !PInvoke.GetMonitorInfo(monitor, (MONITORINFO*)&info))
        {
            return null;
        }

        RECT m = info.monitorInfo.rcMonitor;
        return new WindowView(
            minimized,
            new ScreenRect(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y),
            new ScreenRect(m.left, m.top, m.right, m.bottom),
            RefreshOf(info.szDevice.ToString()));
    }

    /// <summary>The monitor's current refresh rate, or null — 0 and 1 mean "the hardware default" per DEVMODEW, not a number.</summary>
    private static unsafe int? RefreshOf(string device)
    {
        var mode = new DEVMODEW { dmSize = (ushort)sizeof(DEVMODEW) };
        return PInvoke.EnumDisplaySettings(device, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref mode) && mode.dmDisplayFrequency > 1
            ? (int)mode.dmDisplayFrequency
            : null;
    }

    private sealed class Search(IReadOnlyCollection<int> pids)
    {
        public IReadOnlyCollection<int> Pids { get; } = pids;

        public HWND Best { get; set; }

        public long BestArea { get; set; } = -1;
    }
}

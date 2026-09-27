using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Display;

namespace FrameLedger.Application.Recording;

/// <summary>
/// A session's display facts onto its row (schema 0016, beta.10). On the recorder's SKELETON, not the aggregator's
/// columns: a Tier-2 session never reaches the aggregator, and its window is exactly what it still has to say.
/// </summary>
public static class SessionDisplayColumns
{
    /// <summary><paramref name="row"/> with <paramref name="display"/>'s columns; the row unchanged when nothing was sampled.</summary>
    public static SessionRow WithDisplay(this SessionRow row, DisplaySummary? display)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (display is null)
        {
            return row;
        }

        return row with
        {
            DisplayExclusiveMs = display.ExclusiveMs,
            DisplayBorderlessMs = display.BorderlessMs,
            DisplayWindowedMs = display.WindowedMs,
            DisplayCoversMs = display.CoversMs,
            DisplayMinimizedMs = display.MinimizedMs,
            DisplayNoWindowMs = display.NoWindowMs,
            DisplayChanges = display.Changes,
            DisplaySource = display.Source,
            DisplayWindowW = display.WindowWidth,
            DisplayWindowH = display.WindowHeight,
            DisplayBufferW = display.BufferWidth is { } bw ? (int)Math.Min(bw, int.MaxValue) : null,
            DisplayBufferH = display.BufferHeight is { } bh ? (int)Math.Min(bh, int.MaxValue) : null,
            DisplayMonitorW = display.MonitorWidth,
            DisplayMonitorH = display.MonitorHeight,
            DisplayMonitorHz = display.MonitorHz,
            // sessions.swap_effect had a column since 0001 and no writer: the swap chain's own description is one now.
            SwapEffect = display.SwapEffect ?? row.SwapEffect,
        };
    }

    /// <summary>The row's display facts back as a summary, or null for a row that has none (written before, or never sampled).</summary>
    public static DisplaySummary? DisplayOf(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.DisplayExclusiveMs is null && row.DisplayWindowedMs is null && row.DisplayNoWindowMs is null)
        {
            return null;
        }

        return new DisplaySummary
        {
            ExclusiveMs = row.DisplayExclusiveMs ?? 0,
            BorderlessMs = row.DisplayBorderlessMs ?? 0,
            WindowedMs = row.DisplayWindowedMs ?? 0,
            CoversMs = row.DisplayCoversMs ?? 0,
            MinimizedMs = row.DisplayMinimizedMs ?? 0,
            NoWindowMs = row.DisplayNoWindowMs ?? 0,
            Changes = (int)(row.DisplayChanges ?? 0),
            Source = row.DisplaySource,
            WindowWidth = row.DisplayWindowW,
            WindowHeight = row.DisplayWindowH,
            BufferWidth = row.DisplayBufferW is { } bw ? (uint)bw : null,
            BufferHeight = row.DisplayBufferH is { } bh ? (uint)bh : null,
            MonitorWidth = row.DisplayMonitorW,
            MonitorHeight = row.DisplayMonitorH,
            MonitorHz = row.DisplayMonitorHz,
            SwapEffect = row.SwapEffect,
        };
    }
}

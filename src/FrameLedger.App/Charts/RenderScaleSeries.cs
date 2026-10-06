// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Charts;

/// <summary>
/// The render scale over a session (beta.14, D49): <c>100 × √(render pixels / output pixels)</c> — the per-axis scale
/// <c>03_METRICS</c> §Upscaling names, so DLSS Quality's 1707×960 of 2560×1440 reads 66.7 %. Per present from the
/// <c>render_res</c> blob where the resolution varied, a flat line at the row's extent where it did not (the finalizer stores
/// the blob only then), and nothing where the upscaler's parameters were never measured.
/// </summary>
internal static class RenderScaleSeries
{
    /// <summary>The scale in percent, or null when either size is unknown (0 is the writer's "unknown").</summary>
    public static double? Scale(int renderW, int renderH, int outputW, int outputH) =>
        renderW > 0 && renderH > 0 && outputW > 0 && outputH > 0
            ? 100.0 * Math.Sqrt((double)renderW * renderH / ((double)outputW * outputH))
            : null;

    /// <summary>
    /// Seconds from the first present and the scale there — only the points where the scale changes, and the last one, since
    /// a scale holds until the next change and a 300,000-present session has a handful (drawn as steps). Empty when nothing
    /// was measured.
    /// </summary>
    public static (double[] Xs, double[] Ys) Of(SessionSeries? series, SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (series is null || series.Presents == 0)
        {
            return ([], []);
        }

        if (series.RenderRes is { } quads && quads.Length >= 4 * series.Presents)
        {
            return Changes(series, quads);
        }

        return Scale(row.RenderW ?? 0, row.RenderH ?? 0, row.OutputW ?? 0, row.OutputH ?? 0) is double flat
            ? ([0, series.DurationS], [flat, flat])
            : ([], []);
    }

    private static (double[] Xs, double[] Ys) Changes(SessionSeries series, ushort[] quads)
    {
        var xs = new List<double>();
        var ys = new List<double>();
        double? last = null;
        int lastIndex = -1;
        for (int i = 0; i < series.Presents; i++)
        {
            if (Scale(quads[4 * i], quads[4 * i + 1], quads[4 * i + 2], quads[4 * i + 3]) is not double s)
            {
                continue;
            }

            lastIndex = i;
            if (last is double previous && Math.Abs(previous - s) < 1e-9)
            {
                continue;
            }

            xs.Add(series.TimesS[i]);
            ys.Add(s);
            last = s;
        }

        // The last measured present closes the final step, so a scale held to the end is drawn to the end.
        if (last is double end && xs.Count > 0 && series.TimesS[lastIndex] > xs[^1])
        {
            xs.Add(series.TimesS[lastIndex]);
            ys.Add(end);
        }

        return ([.. xs], [.. ys]);
    }
}

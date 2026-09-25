using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using FrameLedger.App.Charts;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.App.Services;

/// <summary>
/// FR-9: the per-frame CSV under <c>03_METRICS</c> §Export schema (its column list, its <c>#</c> header block,
/// every column from a stored source, InvariantCulture throughout — a file must read the same in every UI
/// language), and the session JSON (metadata + aggregates + segments). A Tier-2 session exports no per-frame
/// rows at all; the header says why.
/// </summary>
public sealed class SessionExporter
{
    /// <summary>The CSV columns, verbatim from <c>03_METRICS</c> §Export schema.</summary>
    public const string CsvColumns = "frame_index,qpc_ms,frametime_ms,native_or_generated,render_w,render_h,output_w,output_h,upscaler,upscaler_quality,fg_mode,rt_flags,dispatch_rays,pso_created,vram_mb,reflex_latency_us";

    private static readonly CultureInfo _inv = CultureInfo.InvariantCulture;

    public static void WriteCsv(TextWriter writer, SessionRow row, GameRow game, HardwareSnapshot? hardware, IReadOnlyList<SegmentRow> segments, SessionSeries? series)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(segments);
        WriteHeader(writer, row, game, hardware, segments);
        writer.WriteLine(CsvColumns);
        if (row.Tier != CaptureTier.Hooked || series is null)
        {
            return;
        }

        SegmentRow[] byFrame = [.. segments.OrderBy(static s => s.StartFrame)];
        for (int i = 0; i < series.Presents; i++)
        {
            uint frameIndex = series.FrameIndex is { } fi && i < fi.Length ? fi[i] : (uint)i;
            SegmentRow? segment = SegmentAt(byFrame, frameIndex);
            (string rw, string rh, string ow, string oh) = Resolution(series, i, segment);
            writer.WriteLine(string.Join(',',
                frameIndex.ToString(_inv),
                (series.TimesS[i] * 1000.0).ToString("0.###", _inv),
                series.FrameTimesMs[i].ToString("0.###", _inv),
                series.Generated[i] ? "generated" : "native",
                rw, rh, ow, oh,
                segment?.Upscaler ?? string.Empty,
                segment?.UpscalerQuality ?? string.Empty,
                segment?.FgMode ?? string.Empty,
                Cell(series.RtFlags, i),
                Cell(series.DispatchRays, i),
                Cell(series.PsoCreated, i),
                Cell(series.VramProcMb, i),
                Cell(series.LatencyUs, i)));
        }
    }

    public static SessionExportDocument Document(SessionRow row, GameRow game, HardwareSnapshot? hardware, IReadOnlyList<SegmentRow> segments, SessionAnnotation? annotation)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(segments);
        return new SessionExportDocument
        {
            Schema = "frameledger-session/1",
            SessionGuid = row.SessionGuid,
            Game = game.Name,
            ExePath = game.Fingerprint.ExePath,
            StartedAt = row.StartedAt,
            EndedAt = row.EndedAt,
            DurationSeconds = row.DurationSeconds,
            CaptureTier = (int)row.Tier,
            CaptureMode = row.Mode.ToString(),
            ExitStatus = Vocabulary.ExitStatusText(row.ExitStatus),
            Api = row.Api,
            PresentMode = row.PresentMode,
            Hardware = hardware,
            Aggregates = row,
            Segments = segments,
            Tags = annotation?.Tags ?? [],
            Notes = annotation?.Notes,
        };
    }

    public static void WriteJson(Stream stream, SessionExportDocument document)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(document);
        JsonSerializer.Serialize(stream, document, AppJsonContext.Default.SessionExportDocument);
    }

    private static void WriteHeader(TextWriter w, SessionRow row, GameRow game, HardwareSnapshot? hardware, IReadOnlyList<SegmentRow> segments)
    {
        w.WriteLine("# FrameLedger session export (03_METRICS §Export schema)");
        w.WriteLine("# game: " + game.Name);
        w.WriteLine("# exe: " + game.Fingerprint.ExePath);
        w.WriteLine("# started_at: " + row.StartedAt.ToString("O", _inv));
        w.WriteLine("# ended_at: " + row.EndedAt.ToString("O", _inv));
        w.WriteLine("# capture_tier: " + ((int)row.Tier).ToString(_inv) + (row.Tier == CaptureTier.Hooked ? " (hooked, measured)" : " (not hooked: duration and sensors only; no per-frame rows)"));
        w.WriteLine("# api: " + (row.Api ?? "N/A") + "; present_mode: " + (row.PresentMode ?? "N/A") + "; swap_effect: " + (row.SwapEffect ?? "N/A"));
        w.WriteLine("# hardware: " + (hardware is null ? "N/A" : string.Join("; ", new[] { hardware.CpuName, hardware.GpuName, hardware.GpuDriver, hardware.OsBuild, hardware.DisplayRes }.Where(static s => !string.IsNullOrEmpty(s)))));
        w.WriteLine("# rt: " + row.RtFlag + " (" + (row.RtSource ?? "n/a") + "); pt: " + row.PtFlag + " (" + (row.PtSource ?? "n/a") + "); rr: " + row.RrFlag + " (" + (row.RrSource ?? "n/a") + ")");
        w.WriteLine("# fg_mode: " + row.FgMode + "; fg_source: " + (row.FgSource ?? "n/a") + "; presented_qualifier: " + (row.PresentedQualifier ?? "n/a"));
        foreach (SegmentRow s in segments)
        {
            w.WriteLine("# segment: frames " + s.StartFrame.ToString(_inv) + "-" + s.EndFrame.ToString(_inv) + "; " + (s.Upscaler ?? "n/a") + " " + (s.UpscalerQuality ?? string.Empty) + "; " + (s.RenderW?.ToString(_inv) ?? "?") + "x" + (s.RenderH?.ToString(_inv) ?? "?") + " -> " + (s.OutputW?.ToString(_inv) ?? "?") + "x" + (s.OutputH?.ToString(_inv) ?? "?") + "; fg " + (s.FgMode ?? "n/a"));
        }

        w.WriteLine("# note: vram_mb is a held 1 Hz sample, not a per-frame measurement; qpc_ms is relative to the first present");
        w.WriteLine("# note: one swapchain's presents, the stream the statistics are over; qpc_ms closes up every gap (frametime_ms 0)");
    }

    private static SegmentRow? SegmentAt(SegmentRow[] byFrame, uint frameIndex)
    {
        SegmentRow? found = null;
        foreach (SegmentRow s in byFrame)
        {
            if (s.StartFrame <= frameIndex && frameIndex <= s.EndFrame)
            {
                found = s;
            }
        }

        return found;
    }

    private static (string, string, string, string) Resolution(SessionSeries series, int i, SegmentRow? segment)
    {
        if (series.RenderRes is { } rr && (i * 4) + 3 < rr.Length)
        {
            return (rr[i * 4].ToString(_inv), rr[(i * 4) + 1].ToString(_inv), rr[(i * 4) + 2].ToString(_inv), rr[(i * 4) + 3].ToString(_inv));
        }

        return (segment?.RenderW?.ToString(_inv) ?? string.Empty, segment?.RenderH?.ToString(_inv) ?? string.Empty,
            segment?.OutputW?.ToString(_inv) ?? string.Empty, segment?.OutputH?.ToString(_inv) ?? string.Empty);
    }

    private static string Cell<T>(T[]? values, int i)
        where T : struct, IFormattable =>
        values is not null && i < values.Length ? values[i].ToString(null, _inv) : string.Empty;
}

using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FrameLedger.App.Services;

/// <summary>
/// The four tray icons, drawn rather than shipped: a rounded tile with the state glyph — a ring (idle), a filled
/// dot (● capturing), a half dot (◐ recording only, nothing measured), two bars (⏸ paused). Drawn with WPF at
/// 32 px, encoded as a PNG inside a one-entry <c>.ico</c> stream and handed to the shell as a
/// <see cref="Icon"/> — H.NotifyIcon 2.4.1 converts only file-backed image sources itself (measured 2026-09-14:
/// a <c>RenderTargetBitmap</c> as <c>IconSource</c> throws <c>NotImplementedException</c> on the dispatcher).
/// Colours are fixed, not theme brushes: the tray is the system's surface.
/// </summary>
public static class TrayIcons
{
    private const int _size = 32;

    public static Icon For(TrayState state)
    {
        byte[] png = Png(state);
        using var stream = new MemoryStream(22 + png.Length);
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            // ICONDIR: reserved, type 1 (icon), one image; ICONDIRENTRY: width, height, no palette, reserved,
            // one plane, 32 bpp, the PNG's size, its offset. Vista+ reads PNG-compressed entries.
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)1);
            writer.Write((byte)_size);
            writer.Write((byte)_size);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(png.Length);
            writer.Write(22);
            writer.Write(png);
        }

        stream.Position = 0;
        return new Icon(stream);
    }

    private static byte[] Png(TrayState state)
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1F, 0x2A, 0x3A)), null, new Rect(0, 0, _size, _size), 7, 7);
            System.Windows.Media.Brush glyph = new SolidColorBrush(state switch
            {
                TrayState.Capturing => System.Windows.Media.Color.FromRgb(0x3F, 0xC8, 0x6B),
                TrayState.RecordingOnly => System.Windows.Media.Color.FromRgb(0xE8, 0xB0, 0x3A),
                TrayState.Paused => System.Windows.Media.Color.FromRgb(0xC9, 0xD1, 0xD9),
                _ => System.Windows.Media.Color.FromRgb(0x8A, 0x99, 0xA8),
            });
            var centre = new System.Windows.Point(_size / 2.0, _size / 2.0);
            const double r = 8;
            switch (state)
            {
                case TrayState.Capturing:
                    dc.DrawEllipse(glyph, null, centre, r, r);
                    break;
                case TrayState.RecordingOnly:
                    dc.DrawEllipse(null, new System.Windows.Media.Pen(glyph, 2.5), centre, r, r);
                    dc.DrawGeometry(glyph, null, HalfDisc(centre, r));
                    break;
                case TrayState.Paused:
                    dc.DrawRectangle(glyph, null, new Rect(centre.X - 7, centre.Y - 8, 5, 16));
                    dc.DrawRectangle(glyph, null, new Rect(centre.X + 2, centre.Y - 8, 5, 16));
                    break;
                default:
                    dc.DrawEllipse(null, new System.Windows.Media.Pen(glyph, 3), centre, r, r);
                    break;
            }
        }

        var bitmap = new RenderTargetBitmap(_size, _size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var png = new MemoryStream();
        encoder.Save(png);
        return png.ToArray();
    }

    private static PathGeometry HalfDisc(System.Windows.Point centre, double r)
    {
        var figure = new PathFigure { StartPoint = new System.Windows.Point(centre.X, centre.Y - r), IsClosed = true };
        figure.Segments.Add(new ArcSegment(new System.Windows.Point(centre.X, centre.Y + r), new System.Windows.Size(r, r), 0, false, SweepDirection.Clockwise, true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }
}

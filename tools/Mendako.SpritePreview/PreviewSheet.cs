using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mendako.SpritePreview;

/// <summary>一覧の 1 マス。<paramref name="Draw"/> は (0, 0)-(Width, Height) の範囲に描く。</summary>
internal sealed record PreviewCell(string Label, int Width, int Height, Action<DrawingContext> Draw);

/// <summary>見出しつきのマスの集まり。行ごとに左から並べ、同じ列の幅は揃える。</summary>
internal sealed record PreviewSection(string Title, IReadOnlyList<IReadOnlyList<PreviewCell>> Rows);

/// <summary>見出しとマスを 1 枚の画像に並べる。</summary>
internal static class PreviewSheet
{
    private const int Margin = 16;
    private const int Gap = 12;
    private const int LabelHeight = 30;
    private const double LabelSize = 11d;
    private const double TitleSize = 15d;

    private static readonly Brush PageBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF7, 0xF6, 0xF2)));
    private static readonly Brush CellBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x9C, 0xA8, 0xBA)));
    private static readonly Brush TextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x38)));
    private static readonly Typeface Face = new("Yu Gothic UI");

    public static BitmapSource Render(string title, IReadOnlyList<PreviewSection> sections, int zoom)
    {
        var visual = new DrawingVisual();

        // 拡大時にぼかすとドットの段差が見えなくなり、確認の意味がなくなる
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        RenderOptions.SetEdgeMode(visual, EdgeMode.Aliased);

        var layout = Layout(title, sections);

        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(PageBrush, null, new Rect(0, 0, layout.Width, layout.Height));
            foreach (var item in layout.Items)
            {
                item(dc);
            }
        }

        var bitmap = new RenderTargetBitmap(layout.Width, layout.Height, 96d, 96d, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();

        return zoom > 1 ? Pixels.Upscale(bitmap, zoom) : bitmap;
    }

    public static void SavePng(BitmapSource bitmap, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static (int Width, int Height, List<Action<DrawingContext>> Items) Layout(
        string title,
        IReadOnlyList<PreviewSection> sections)
    {
        var items = new List<Action<DrawingContext>>();
        var y = Margin;
        var width = Margin;

        items.Add(dc => DrawText(dc, title, Margin, Margin, TitleSize, FontWeights.Bold));
        y += 28;

        foreach (var section in sections)
        {
            var top = y + 8;
            items.Add(dc => DrawText(dc, section.Title, Margin, top, TitleSize - 2, FontWeights.SemiBold));
            y = top + 26;

            var columns = section.Rows.Count == 0 ? 0 : section.Rows.Max(r => r.Count);
            var columnWidths = Enumerable.Range(0, columns)
                .Select(c => section.Rows.Where(r => r.Count > c).Max(r => r[c].Width))
                .ToArray();

            foreach (var row in section.Rows)
            {
                var x = Margin;
                var rowHeight = row.Count == 0 ? 0 : row.Max(c => c.Height);

                for (var c = 0; c < row.Count; c++)
                {
                    var cell = row[c];
                    var cellX = x;
                    var cellY = y;
                    var cellWidth = columnWidths[c];

                    items.Add(dc =>
                    {
                        dc.DrawRectangle(CellBrush, null, new Rect(cellX, cellY, cellWidth, rowHeight));

                        // 中身はマスの中で下揃え・左右中央。接地位置を揃えて見比べられるようにする
                        var offsetX = cellX + ((cellWidth - cell.Width) / 2);
                        var offsetY = cellY + (rowHeight - cell.Height);
                        dc.PushTransform(new TranslateTransform(offsetX, offsetY));
                        cell.Draw(dc);
                        dc.Pop();

                        DrawText(dc, cell.Label, cellX, cellY + rowHeight + 2, LabelSize, FontWeights.Normal, cellWidth);
                    });

                    x += cellWidth + Gap;
                }

                width = Math.Max(width, x - Gap + Margin);
                y += rowHeight + LabelHeight + Gap;
            }
        }

        return (width, y + Margin, items);
    }

    private static void DrawText(
        DrawingContext dc,
        string text,
        double x,
        double y,
        double size,
        FontWeight weight,
        double maxWidth = 0d)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.GetCultureInfo("ja-JP"),
            FlowDirection.LeftToRight,
            new Typeface(Face.FontFamily, FontStyles.Normal, weight, FontStretches.Normal),
            size,
            TextBrush,
            null,
            TextFormattingMode.Display,
            1d);

        if (maxWidth > 0d)
        {
            formatted.MaxTextWidth = maxWidth;
        }

        dc.DrawText(formatted, new Point(x, y));
    }

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}

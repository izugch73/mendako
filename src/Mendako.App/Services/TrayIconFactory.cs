using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Mendako.App.Services;

/// <summary>
/// トレイアイコンを実行時に描き起こす。.ico をリポジトリに置かずに済み、
/// 起きている / 寝ているで見た目を変えられる。
/// </summary>
public static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon Create(int size = 32, bool asleep = false)
    {
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);

        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var s = size / 32f;

            using var finBrush = new SolidBrush(Color.FromArgb(255, 236, 155, 169));
            g.FillEllipse(finBrush, 1.0f * s, 3.5f * s, 11f * s, 7.5f * s);
            g.FillEllipse(finBrush, 20f * s, 3.5f * s, 11f * s, 7.5f * s);

            using var bodyBrush = new LinearGradientBrush(
                new RectangleF(5f * s, 6f * s, 22f * s, 22f * s),
                Color.FromArgb(255, 246, 173, 186),
                Color.FromArgb(255, 224, 132, 148),
                LinearGradientMode.Vertical);
            g.FillEllipse(bodyBrush, 5f * s, 6f * s, 22f * s, 21f * s);

            if (asleep)
            {
                using var linePen = new Pen(Color.FromArgb(255, 58, 43, 51), Math.Max(1f, 1.6f * s));
                linePen.StartCap = LineCap.Round;
                linePen.EndCap = LineCap.Round;
                g.DrawArc(linePen, 9f * s, 12f * s, 6f * s, 6f * s, 20f, 140f);
                g.DrawArc(linePen, 17f * s, 12f * s, 6f * s, 6f * s, 20f, 140f);
            }
            else
            {
                using var eyeBrush = new SolidBrush(Color.FromArgb(255, 58, 43, 51));
                g.FillEllipse(eyeBrush, 10f * s, 13f * s, 4.5f * s, 5.5f * s);
                g.FillEllipse(eyeBrush, 17.5f * s, 13f * s, 4.5f * s, 5.5f * s);

                using var glintBrush = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
                g.FillEllipse(glintBrush, 11f * s, 14f * s, 1.8f * s, 1.8f * s);
                g.FillEllipse(glintBrush, 18.5f * s, 14f * s, 1.8f * s, 1.8f * s);
            }
        }

        // GetHicon が返すハンドルは呼び出し側の所有物なので、複製してから必ず破棄する
        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}

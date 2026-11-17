using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mendako.Core.Model;

namespace Mendako.App.Views;

/// <summary>
/// タスクバーに置いたときのアイコン。持ち物ウィンドウの 128px の絵をそのまま縮めると、
/// メンダコの粗いドット絵と並べたときに絵柄の細かさが揃わない。
/// 16×16 の粗いドットに落とし、メンダコと同じ濃紺で 1 ドットの縁取りを付けて、整数倍で拡大する。
/// 縁取りは見た目を揃えるだけでなく、白いアイテム (塩・たまご・牛乳など) が
/// 明るいタスクバーに溶けないようにする役目もある。
/// </summary>
public static class ScatterIcons
{
    /// <summary>元の絵をこのドット数に落とす。8 で割り切れるので、ブロック平均に端数が出ない。</summary>
    private const int SourceDots = 16;

    /// <summary>縁取りを含めた 1 辺のドット数。</summary>
    public const int Dots = SourceDots + 2;

    /// <summary>この透明度 (0-255) 以上のブロックだけを絵のドットにする。半透明のドットはドット絵には無いので 2 値にする。</summary>
    private const int OpaqueThreshold = 110;

    private static readonly Dictionary<ItemId, ScatterIcon> Cache = new();

    public static ScatterIcon Get(ItemId id)
    {
        if (Cache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var icon = Build(ItemIcons.Get(id));
        Cache[id] = icon;
        return icon;
    }

    private static ScatterIcon Build(ImageSource source)
    {
        var converted = new FormatConvertedBitmap((BitmapSource)source, PixelFormats.Bgra32, null, 0d);
        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[height * stride];
        converted.CopyPixels(pixels, stride, 0);

        var block = width / SourceDots;
        var blockRows = height / SourceDots;
        var opaque = new bool[Dots * Dots];
        var colors = new (byte B, byte G, byte R)[Dots * Dots];

        for (var dy = 0; dy < SourceDots; dy++)
        {
            for (var dx = 0; dx < SourceDots; dx++)
            {
                long sumA = 0, sumB = 0, sumG = 0, sumR = 0;
                for (var y = dy * blockRows; y < (dy + 1) * blockRows; y++)
                {
                    for (var x = dx * block; x < (dx + 1) * block; x++)
                    {
                        var offset = (y * stride) + (x * 4);
                        int a = pixels[offset + 3];
                        sumA += a;
                        sumB += pixels[offset] * a;
                        sumG += pixels[offset + 1] * a;
                        sumR += pixels[offset + 2] * a;
                    }
                }

                var cells = block * blockRows;
                if (sumA / cells < OpaqueThreshold)
                {
                    continue;
                }

                // 縁取りの分、1 ドットずらして置く
                var index = ((dy + 1) * Dots) + dx + 1;
                opaque[index] = true;
                colors[index] = ((byte)(sumB / sumA), (byte)(sumG / sumA), (byte)(sumR / sumA));
            }
        }

        var outline = new bool[Dots * Dots];
        for (var y = 0; y < Dots; y++)
        {
            for (var x = 0; x < Dots; x++)
            {
                if (opaque[(y * Dots) + x])
                {
                    continue;
                }

                outline[(y * Dots) + x] =
                    IsSet(opaque, x - 1, y) || IsSet(opaque, x + 1, y) || IsSet(opaque, x, y - 1) || IsSet(opaque, x, y + 1);
            }
        }

        var output = new byte[Dots * Dots * 4];
        var solid = new bool[Dots * Dots];
        var lowest = 0;
        for (var i = 0; i < Dots * Dots; i++)
        {
            if (!opaque[i] && !outline[i])
            {
                continue;
            }

            solid[i] = true;
            lowest = Math.Max(lowest, i / Dots);

            var (b, g, r) = opaque[i] ? colors[i] : OutlineColor;
            output[i * 4] = b;
            output[(i * 4) + 1] = g;
            output[(i * 4) + 2] = r;
            output[(i * 4) + 3] = 255;
        }

        var bitmap = BitmapSource.Create(Dots, Dots, 96d, 96d, PixelFormats.Bgra32, null, output, Dots * 4);
        bitmap.Freeze();
        return new ScatterIcon(bitmap, solid, Dots - 1 - lowest);
    }

    /// <summary>メンダコの輪郭と同じ濃紺 (BGR)。</summary>
    private static readonly (byte B, byte G, byte R) OutlineColor = (64, 36, 27);

    private static bool IsSet(bool[] dots, int x, int y) =>
        x >= 0 && y >= 0 && x < Dots && y < Dots && dots[(y * Dots) + x];
}

/// <summary>タスクバーに置くアイコン 1 個。</summary>
/// <param name="Bitmap"><see cref="ScatterIcons.Dots"/> 四方のドット絵。</param>
/// <param name="Solid">絵のあるドット。当たり判定に使う。行ごとに左から並ぶ。</param>
/// <param name="BottomPaddingDots">絵の下にある空のドット数。接地するとき、この分だけ下へずらして絵の底を足場に合わせる。</param>
public sealed record ScatterIcon(BitmapSource Bitmap, bool[] Solid, int BottomPaddingDots)
{
    public bool IsSolid(int x, int y) =>
        x >= 0 && y >= 0 && x < ScatterIcons.Dots && y < ScatterIcons.Dots && Solid[(y * ScatterIcons.Dots) + x];
}

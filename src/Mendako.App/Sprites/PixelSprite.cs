using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mendako.App.Sprites;

/// <summary>
/// 文字列で書いたドット絵を <see cref="BitmapSource"/> に展開する。
/// PNG を持たずに済むので、バイナリ資産なしという方針を崩さずにドット絵化できる。
/// </summary>
public static class PixelSprite
{
    /// <summary>透明を表す文字。</summary>
    public const char Transparent = '.';

    /// <summary>
    /// ドットの行とパレットからビットマップを作る。
    /// 生成物は Freeze 済みなので、キャッシュして使い回してよい。
    /// </summary>
    public static BitmapSource Create(IReadOnlyList<string> rows, IReadOnlyDictionary<char, Color> palette)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(palette);

        if (rows.Count == 0)
        {
            throw new ArgumentException("行が空です。", nameof(rows));
        }

        var height = rows.Count;
        var width = rows[0].Length;
        var stride = width * 4;
        var pixels = new byte[height * stride];

        for (var y = 0; y < height; y++)
        {
            var row = rows[y];
            if (row.Length != width)
            {
                throw new ArgumentException(
                    $"{y} 行目の長さが {row.Length} です。全行を {width} に揃えてください。",
                    nameof(rows));
            }

            for (var x = 0; x < width; x++)
            {
                var key = row[x];
                if (key == Transparent)
                {
                    continue;
                }

                if (!palette.TryGetValue(key, out var color))
                {
                    throw new ArgumentException(
                        $"パレットに '{key}' がありません ({x}, {y})。",
                        nameof(palette));
                }

                var offset = (y * stride) + (x * 4);
                pixels[offset + 0] = color.B;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.R;
                pixels[offset + 3] = color.A;
            }
        }

        var bitmap = BitmapSource.Create(
            width,
            height,
            96d,
            96d,
            PixelFormats.Bgra32,
            palette: null,
            pixels: pixels,
            stride: stride);

        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>ドット 1 文字を書き換える。フレーム合成に使う。</summary>
    public static void Set(string[] rows, int x, int y, char value)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (y < 0 || y >= rows.Length)
        {
            return;
        }

        var row = rows[y];
        if (x < 0 || x >= row.Length)
        {
            return;
        }

        rows[y] = string.Concat(row.AsSpan(0, x), value.ToString(), row.AsSpan(x + 1));
    }
}

namespace Mendako.IconGen;

/// <summary>ドットの行を BGRA のバイト列に展開し、整数倍に拡大する。</summary>
internal static class PixelGrid
{
    /// <summary>1 ドット = 4 バイト (B, G, R, A) の順。ICO の DIB がこの並びなので合わせてある。</summary>
    public const int BytesPerPixel = 4;

    public static byte[] Expand(IReadOnlyList<string> rows, IReadOnlyDictionary<char, Rgb> palette)
    {
        var height = rows.Count;
        var width = rows[0].Length;
        var pixels = new byte[width * height * BytesPerPixel];

        for (var y = 0; y < height; y++)
        {
            var row = rows[y];
            if (row.Length != width)
            {
                throw new InvalidOperationException(
                    $"{y} 行目の長さが {row.Length} です。全行を {width} に揃えてください。");
            }

            for (var x = 0; x < width; x++)
            {
                var key = row[x];
                if (key == IconArt.Transparent)
                {
                    continue;
                }

                if (!palette.TryGetValue(key, out var color))
                {
                    throw new InvalidOperationException($"パレットに '{key}' がありません ({x}, {y})。");
                }

                var offset = ((y * width) + x) * BytesPerPixel;
                pixels[offset + 0] = color.B;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.R;
                pixels[offset + 3] = 0xFF;
            }
        }

        return pixels;
    }

    /// <summary>最近傍でドットを複製して拡大する。端数倍率は扱わない。</summary>
    public static byte[] Scale(byte[] source, int sourceSize, int factor)
    {
        var size = sourceSize * factor;
        var scaled = new byte[size * size * BytesPerPixel];

        for (var y = 0; y < size; y++)
        {
            var sourceRow = (y / factor) * sourceSize * BytesPerPixel;
            for (var x = 0; x < size; x++)
            {
                var from = sourceRow + ((x / factor) * BytesPerPixel);
                var to = ((y * size) + x) * BytesPerPixel;
                scaled[to + 0] = source[from + 0];
                scaled[to + 1] = source[from + 1];
                scaled[to + 2] = source[from + 2];
                scaled[to + 3] = source[from + 3];
            }
        }

        return scaled;
    }
}

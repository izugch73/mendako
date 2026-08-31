namespace Mendako.IconGen;

/// <summary>複数サイズを束ねた .ico を書き出す。</summary>
internal static class IcoWriter
{
    /// <summary>ICONDIR (6 バイト) に続く ICONDIRENTRY 1 件の大きさ。</summary>
    private const int EntrySize = 16;

    /// <summary>.ico に入れる 1 コマ。</summary>
    /// <param name="Size">一辺のドット数。</param>
    /// <param name="Bgra">左上から右下へ並んだ BGRA。</param>
    /// <param name="UsePng">true なら PNG、false なら非圧縮の DIB で格納する。</param>
    public sealed record Image(int Size, byte[] Bgra, bool UsePng);

    public static void Write(string path, IReadOnlyList<Image> images)
    {
        var payloads = images
            .Select(image => image.UsePng
                ? PngEncoder.Encode(image.Size, image.Bgra)
                : DibPayload(image.Size, image.Bgra))
            .ToList();

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0); // 予約
        writer.Write((ushort)1); // 1 = アイコン
        writer.Write((ushort)images.Count);

        var offset = 6 + (EntrySize * images.Count);
        for (var i = 0; i < images.Count; i++)
        {
            // 256 は 1 バイトに入らないので 0 で表す仕様
            var dimension = (byte)(images[i].Size >= 256 ? 0 : images[i].Size);
            writer.Write(dimension);
            writer.Write(dimension);
            writer.Write((byte)0); // パレット数。32bpp なので 0
            writer.Write((byte)0); // 予約
            writer.Write((ushort)1);  // プレーン数
            writer.Write((ushort)32); // ビット深度
            writer.Write(payloads[i].Length);
            writer.Write(offset);
            offset += payloads[i].Length;
        }

        foreach (var payload in payloads)
        {
            writer.Write(payload);
        }
    }

    /// <summary>
    /// BITMAPINFOHEADER + XOR ビットマップ + AND マスク。
    /// 32bpp なので実際の透過はアルファが担うが、古い描画経路のために AND マスクも正しく置く。
    /// </summary>
    private static byte[] DibPayload(int size, byte[] bgra)
    {
        var rowBytes = size * PixelGrid.BytesPerPixel;
        var maskStride = ((size + 31) / 32) * 4; // 1bpp、行は 4 バイト境界
        var xorSize = rowBytes * size;
        var andSize = maskStride * size;

        using var stream = new MemoryStream(40 + xorSize + andSize);
        using var writer = new BinaryWriter(stream);

        writer.Write(40);            // biSize
        writer.Write(size);          // biWidth
        writer.Write(size * 2);      // biHeight。XOR と AND を縦に積むので 2 倍で申告する
        writer.Write((ushort)1);     // biPlanes
        writer.Write((ushort)32);    // biBitCount
        writer.Write(0);             // biCompression = BI_RGB
        writer.Write(xorSize + andSize); // biSizeImage
        writer.Write(0);             // biXPelsPerMeter
        writer.Write(0);             // biYPelsPerMeter
        writer.Write(0);             // biClrUsed
        writer.Write(0);             // biClrImportant

        // DIB は下から上へ並べる
        for (var y = size - 1; y >= 0; y--)
        {
            writer.Write(bgra, y * rowBytes, rowBytes);
        }

        var maskRow = new byte[maskStride];
        for (var y = size - 1; y >= 0; y--)
        {
            Array.Clear(maskRow);
            for (var x = 0; x < size; x++)
            {
                var alpha = bgra[(((y * size) + x) * PixelGrid.BytesPerPixel) + 3];
                if (alpha == 0)
                {
                    // ビットが立っている = 透明
                    maskRow[x / 8] |= (byte)(0x80 >> (x % 8));
                }
            }

            writer.Write(maskRow);
        }

        writer.Flush();
        return stream.ToArray();
    }
}

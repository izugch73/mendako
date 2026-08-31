using System.IO.Compression;

namespace Mendako.IconGen;

/// <summary>
/// BGRA のバイト列を PNG に符号化する。
///
/// .ico に大きなコマを非圧縮の DIB で入れると 256 x 256 だけで 270 KB になり、
/// ランタイム依存ビルドの exe (約 250 KB) より大きくなってしまう。
/// Vista 以降の .ico は PNG のコマを持てるので、大きいものは PNG で入れる。
/// 実測で 256 x 256 が 1 KB 未満に収まる。
/// </summary>
internal static class PngEncoder
{
    private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Encode(int size, byte[] bgra)
    {
        using var stream = new MemoryStream();
        stream.Write(Signature);

        var header = new byte[13];
        WriteBigEndian(header, 0, size);
        WriteBigEndian(header, 4, size);
        header[8] = 8;  // ビット深度
        header[9] = 6;  // カラータイプ 6 = RGBA
        header[10] = 0; // 圧縮方式は deflate のみ
        header[11] = 0; // フィルタ方式
        header[12] = 0; // インタレースなし

        WriteChunk(stream, "IHDR", header);
        WriteChunk(stream, "IDAT", Deflate(size, bgra));
        WriteChunk(stream, "IEND", Array.Empty<byte>());

        return stream.ToArray();
    }

    private static byte[] Deflate(int size, byte[] bgra)
    {
        // 各行の先頭にフィルタ種別のバイトが要る。ベタ塗りなので 0 (フィルタなし) で十分縮む。
        var raw = new byte[size * ((size * PixelGrid.BytesPerPixel) + 1)];
        var offset = 0;

        for (var y = 0; y < size; y++)
        {
            raw[offset++] = 0;
            for (var x = 0; x < size; x++)
            {
                var from = ((y * size) + x) * PixelGrid.BytesPerPixel;
                raw[offset++] = bgra[from + 2];
                raw[offset++] = bgra[from + 1];
                raw[offset++] = bgra[from + 0];
                raw[offset++] = bgra[from + 3];
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        return compressed.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        stream.Write(length);

        var typeBytes = new byte[4];
        for (var i = 0; i < 4; i++)
        {
            typeBytes[i] = (byte)type[i];
        }

        stream.Write(typeBytes);
        stream.Write(data);

        var crc = Crc32(typeBytes, data);
        var crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, (int)crc);
        stream.Write(crcBytes);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset + 0] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var b in type)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        foreach (var b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];

        for (var i = 0u; i < 256u; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }
}

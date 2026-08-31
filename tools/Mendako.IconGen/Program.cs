namespace Mendako.IconGen;

/// <summary>
/// ドット絵から exe 用の .ico を書き起こす。ビルド時に呼ばれる。
/// リポジトリにバイナリを置かないための道具なので、生成物は obj/ に置く。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 生成するサイズ。すべて <see cref="IconArt.Size"/> の整数倍にすること。
    /// 20 / 24 / 40 のような中途半端なサイズは入れず、Windows 側で縮めさせる。
    /// </summary>
    private static readonly int[] Sizes = { 16, 32, 48, 64, 128, 256 };

    /// <summary>これ以上のサイズは PNG で格納する。小さいコマは互換性を優先して DIB のまま。</summary>
    private const int PngThreshold = 64;

    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("使い方: Mendako.IconGen <出力先.ico>");
            return 1;
        }

        var output = Path.GetFullPath(args[0]);
        var baseImage = PixelGrid.Expand(IconArt.Rows, IconArt.Palette);

        var images = new List<IcoWriter.Image>();
        foreach (var size in Sizes)
        {
            if (size % IconArt.Size != 0)
            {
                Console.Error.WriteLine($"{size} は {IconArt.Size} の整数倍ではありません。");
                return 1;
            }

            var pixels = PixelGrid.Scale(baseImage, IconArt.Size, size / IconArt.Size);
            images.Add(new IcoWriter.Image(size, pixels, size >= PngThreshold));
        }

        var directory = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 途中で失敗した .ico をコンパイラに掴ませないよう、書き切ってから差し替える
        var temporary = output + ".tmp";
        IcoWriter.Write(temporary, images);
        File.Move(temporary, output, overwrite: true);

        Console.WriteLine(
            $"{output} を書きました ({string.Join(" / ", Sizes)}, {new FileInfo(output).Length:N0} バイト)");
        return 0;
    }
}

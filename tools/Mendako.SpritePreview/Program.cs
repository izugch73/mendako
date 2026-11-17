using System;
using System.IO;
using System.Linq;
using System.Threading;
using Mendako.Core.Model;

namespace Mendako.SpritePreview;

/// <summary>
/// メンダコのコマを一覧にして PNG へ書き出す。ドット絵や演出を直したときの目視確認用。
/// </summary>
internal static class Program
{
    private const string Usage =
        "使い方: Mendako.SpritePreview <出力.png> [--sheet frames|motion|accessories|accessories-visual] [--stage Egg|Hatchling|Juvenile|Adult|Elder] [--zoom N] [--section 番号,...]";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
        {
            Console.Error.WriteLine(Usage);
            return 1;
        }

        var output = Path.GetFullPath(args[0]);
        var sheet = "frames";
        var stage = GrowthStage.Adult;
        var zoom = 1;
        int[]? only = null;

        for (var i = 1; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : null;
            var ok = args[i] switch
            {
                "--sheet" when value is not null && Sheets.All.ContainsKey(value) => Assign(ref sheet, value),
                "--stage" when Enum.TryParse<GrowthStage>(value, ignoreCase: true, out var parsed) => Assign(ref stage, parsed),
                "--zoom" when int.TryParse(value, out var z) && z >= 1 => Assign(ref zoom, z),
                "--section" when TryParseNumbers(value, out var numbers) => Assign(ref only, numbers),
                _ => false,
            };

            if (!ok)
            {
                Console.Error.WriteLine($"引数を解釈できません: {args[i]} {value}");
                Console.Error.WriteLine(Usage);
                Console.Error.WriteLine($"一覧の名前: {string.Join(", ", Sheets.All.Keys)}");
                return 1;
            }

            i++;
        }

        var sections = Sheets.All[sheet](stage);
        if (only is not null)
        {
            // 番号は 1 始まり。拡大して細部を見るときに、見たいセクションだけに絞る
            sections = sections.Where((_, index) => only.Contains(index + 1)).ToList();
        }

        var bitmap = PreviewSheet.Render($"Mendako sprite preview: {sheet}", sections, zoom);
        PreviewSheet.SavePng(bitmap, output);

        Console.WriteLine($"{output} を書きました ({bitmap.PixelWidth}×{bitmap.PixelHeight}, {sections.Sum(s => s.Rows.Sum(r => r.Count))} マス)");
        return 0;
    }

    private static bool TryParseNumbers(string? value, out int[] numbers)
    {
        numbers = Array.Empty<int>();
        if (value is null)
        {
            return false;
        }

        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parsed = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out parsed[i]) || parsed[i] < 1)
            {
                return false;
            }
        }

        numbers = parsed;
        return parsed.Length > 0;
    }

    private static bool Assign<T>(ref T target, T value)
    {
        target = value;
        return true;
    }
}

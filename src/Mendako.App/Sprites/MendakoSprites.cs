using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mendako.Core.Behavior;
using Mendako.Core.Model;

namespace Mendako.App.Sprites;

/// <summary>
/// メンダコのドット絵。1 ドット = 1 文字で書いてあり、実行時にビットマップへ展開する。
///
///   .  透明     K  輪郭     R  体     H  ハイライト     D  影     E  濃い影
///   W  白目     S  たまごの斑点
///
/// 頭 (耳ビレを含む上 17 行) と胴 (下 20 行) を別に持ち、
/// 組み合わせ + 目のスタンプでコマを合成している。全コマを手で書くより差分が追いやすい。
/// </summary>
public static class MendakoSprites
{
    public const int Width = 44;

    public const int Height = 37;

    private const int DomeHeight = 17;

    /// <summary>ぺたんとしたときに低くなるドット数。</summary>
    private const int FlatDrop = 6;

    /// <summary>左目の左端。右目は顔の中心で折り返した位置に置く。</summary>
    private const int LeftEyeX = 14;

    private const int EyeTopY = 19;

    /// <summary>視線 1 段ぶんに目をずらすドット数。</summary>
    private const int GazeShift = 2;

    // --- 頭 (0-16 行) ---

    // 頭のてっぺんは細く、その両肩から耳ビレが張り出す。
    // 細い天面と広い耳のあいだにできる段差 (ノッチ) が「耳」に見せる肝で、
    // ここをなだらかにすると途端にただの多角形になる。

    private static readonly string[] DomeUp =
    {
        ".................KKKKKKKKKK.................",
        "..KKK..........KKRRRRRRRRRRKK..........KKK..",
        ".KRRRKK.......KRRRHHRRRRRRRRRK.......KKRRRK.",
        ".KRRRRRKK....KRRHHRRRRRRRRRRRRK....KKRRRRRK.",
        ".KRRRRRRRKKKKRRHRRRRRRRRRRRRRRRKKKKRRRRRRRK.",
        ".KRRRRRRRRRRRRRHRRRRRRRRRRRRRRRRRRRRRRRRRRK.",
        "..KDDRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRDDK..",
        "...KKDDRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRDDKK...",
        ".....KKDRRRRRRRRRRRRRRRRRRRRRRRRRRRRDKK.....",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
    };

    private static readonly string[] DomeMid =
    {
        ".................KKKKKKKKKK.................",
        "...............KKRRRRRRRRRRKK...............",
        "..............KRRRHHRRRRRRRRRK..............",
        ".............KRRHHRRRRRRRRRRRRK.............",
        ".......KKKKKKRRHRRRRRRRRRRRRRRRKKKKKK.......",
        "....KKKRRRRRRRRHRRRRRRRRRRRRRRRRRRRRRKKK....",
        "..KKRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRKK..",
        ".KRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.",
        ".KRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.",
        "..KKDDDRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRDDDKK..",
        "....KKKDDRRRRRRRRRRRRRRRRRRRRRRRRRRDDKKK....",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
    };

    private static readonly string[] DomeDroop =
    {
        ".................KKKKKKKKKK.................",
        "...............KKRRRRRRRRRRKK...............",
        "..............KRRRHHRRRRRRRRRK..............",
        ".............KRRHHRRRRRRRRRRRRK.............",
        "..........KKKRRHRRRRRRRRRRRRRRRKKK..........",
        "........KKRRDRRHRRRRRRRRRRRRRRRDRRKK........",
        "......KKRRRDRRRRRRRRRRRRRRRRRRRRDRRRKK......",
        ".....KRRRRDRRRRRRRRRRRRRRRRRRRRRRDRRRRK.....",
        "....KRRRRDRRRRRRRRRRRRRRRRRRRRRRRRDRRRRK....",
        "....KRRRDRRRRRRRRRRRRRRRRRRRRRRRRRRDRRRK....",
        ".....KKDRRRRRRRRRRRRRRRRRRRRRRRRRRRRDKK.....",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
    };

    // --- 胴 (17-36 行)。目は後からスタンプする ---
    // 耳ビレをはっきり突き出させるため、胴はいちばん膨らんだ行でも耳ビレの先より 5 ドット内側に収めてある

    private static readonly string[] Body =
    {
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        "......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRK......",
        "......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRK......",
        "......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRK......",
        "......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRK......",
        "......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRK......",
        "......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRK......",
        "......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRK......",
        "......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRK......",
        "......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRK......",
        "......KDDDRRRRRRRRRRRRRRRRRRRRRRRRDDDK......",
        ".......KDDDDDDRRRRRRRRRRRRRRRRDDDDDDK.......",
        ".......KDDDDDDDDDDDDDDDDDDDDDDDDDDDDK.......",
        "........KDDDDDDDDDDDDDDDDDDDDDDDDDDK........",
        ".........KDDDDDDDEDDDDDDDDEDDDDDDDK.........",
        "..........KDDDDDDEDDDDDDDDEDDDDDDK..........",
        "..........KDDDDDDKDDDDDDDDKDDDDDDK..........",
        "...........KDDDDK.KDDDDDDK.KDDDDK...........",
        "............KKKK...KKKKKK...KKKK............",
    };

    // --- たまご ---

    private static readonly string[] Egg =
    {
        "...................KKKKKK...................",
        ".................KKRRRRRRKK.................",
        "...............KKRRRRRRRRRRKK...............",
        "..............KRRRRRRRRRRRRRRK..............",
        ".............KRRHRRRRRRRRRRRRRK.............",
        "............KRRHRRRRRRRRRRRRRRRK............",
        "...........KRRHRRRRRRRRRRRRRRRRRK...........",
        "...........KRRHRRRRRRRRRRRRRRRRRK...........",
        "..........KRRHRRRRRRRRRRRRRRRRRRRK..........",
        "..........KRRHRRRRRRRRRRRRRRRRRRRK..........",
        ".........KRRRRRRRRRRRRRRRRRRRRRRRRK.........",
        ".........KRRRRRRRRSSSRRRRRRRRRRRRRK.........",
        "........KRRRRRRRRSSSSSRRRRRRRRRRRRRK........",
        "........KRRRRRRRRSSSSRRRRRRRRRRRRRRK........",
        "........KRRRRRRRRRSSRRRRRRRRRRRRRRRK........",
        "........KRRRRRRRRRRRRRRRRRRRRRRRRRRK........",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRRK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRSSRRRRRDK.......",
        ".......KRRRRRRRRRRRRRRRRRRRSSSRRRRRDK.......",
        ".......KRRRRRRRRRRRRRRRRRRRSSRRRRRRDK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRDK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRDK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRDK.......",
        ".......KRRRRRRRRRRRRRRRRRRRRRRRRRRRDK.......",
        ".......KRRRRRSSRRRRRRRRRRRRRRRRRRRRDK.......",
        "........KRRRSSSRRRRRRRRRRRRRRRRRRRDK........",
        "........KRRSSSSRRRRRRRRRRRRRRRRRRRDK........",
        "........KRRRSSRRRRRRRRRRRRRRRRRRRRDK........",
        ".........KRRRRRRRRRRRRRSSRRRRRRRDDK.........",
        ".........KRRRRRRRRRRRRRSSSRRRRRRDDK.........",
        "..........KDRRRRRRRRRRRRRRRRRRRDDK..........",
        "...........KDRRRRRRRRRRRRRRRRRDDK...........",
        "............KDDRRRRRRRRRRRRRRDDK............",
        ".............KKDDDRRRRRRRRDDDKK.............",
        "...............KKKDDDDDDDDKKK...............",
        "..................KKKKKKKK..................",
    };
    // --- 目 (左目の絵柄。右目は左右反転して使う) ---

    private static readonly string[] OpenEye =
    {
        ".WW.",
        "WWWW",
        "WWWW",
        "WWWW",
        "WWWW",
        ".WW.",
    };

    private static readonly string[] ClosedEye =
    {
        "K....K",
        ".KKKK.",
    };

    private static readonly string[] HappyEye =
    {
        "..KK..",
        ".K..K.",
        "K....K",
    };

    private static readonly string[] SurprisedEye =
    {
        ".WWWW.",
        "WWWWWW",
        "WWWWWW",
        "WWWWWW",
        "WWWWWW",
        ".WWWW.",
    };

    // --- 吹き出し ---

    private static readonly string[] SleepMark =
    {
        "KKK",
        ".K.",
        "KKK",
    };

    private static readonly string[] Heart =
    {
        ".K.K.",
        "KKKKK",
        "KKKKK",
        ".KKK.",
        "..K..",
    };

    private static readonly Color Outline = Color.FromRgb(0x1B, 0x24, 0x40);

    private static readonly Dictionary<(GrowthStage Stage, FinPose Fin, EyePose Eyes, int Gaze, bool Flat), Frame> Cache = new();

    private static Frame? _eggFrame;

    private static BitmapSource? _sleepMark;

    private static BitmapSource? _heart;

    /// <summary>展開済みのコマ。あたり判定に使うのでドットの行も一緒に持つ。</summary>
    public sealed record Frame(BitmapSource Bitmap, IReadOnlyList<string> Rows);

    /// <summary>
    /// 成長段階ごとの 1 ドットの大きさ (DIP)。育つほど大きく見せる。
    /// 整数に限っているのは、端数の倍率だとドットの幅が揃わずにじむため。
    /// その制約のもとで表示サイズを各段階の目安に寄せると、Hatchling と Juvenile、Adult と Elder は同じ倍率になる。
    /// </summary>
    public static int PixelScale(GrowthStage stage) => stage switch
    {
        GrowthStage.Egg => 2,
        GrowthStage.Hatchling => 2,
        GrowthStage.Juvenile => 2,
        GrowthStage.Adult => 3,
        GrowthStage.Elder => 3,
        _ => 3,
    };

    /// <summary>
    /// SleepMark と Heart の 1 ドットの大きさ (DIP)。
    /// <see cref="PixelScale"/> から導かないのは、体のドットの細かさを変えても吹き出しの見た目の大きさは変えないため。
    /// </summary>
    public static int MarkPixelScale(GrowthStage stage) => stage switch
    {
        GrowthStage.Egg => 3,
        GrowthStage.Hatchling => 2,
        GrowthStage.Juvenile => 3,
        GrowthStage.Adult => 4,
        GrowthStage.Elder => 5,
        _ => 4,
    };

    public static Frame Get(GrowthStage stage, FinPose fin, EyePose eyes, int gaze = 0, bool flat = false)
    {
        if (stage == GrowthStage.Egg)
        {
            return _eggFrame ??= new Frame(PixelSprite.Create(Egg, EggPalette()), Egg);
        }

        // 視線が効くのは開いた目だけ。それ以外で鍵を分けるとキャッシュが無駄に増える
        gaze = eyes == EyePose.Open ? Math.Clamp(gaze, -1, 1) : 0;

        var key = (stage, fin, eyes, gaze, flat);
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var rows = Compose(fin, eyes, gaze, flat);
        var frame = new Frame(PixelSprite.Create(rows, Palette(stage)), rows);
        Cache[key] = frame;
        return frame;
    }

    public static BitmapSource GetSleepMark() =>
        _sleepMark ??= PixelSprite.Create(
            SleepMark,
            new Dictionary<char, Color> { ['K'] = Color.FromRgb(0xDC, 0xE4, 0xF0) });

    public static BitmapSource GetHeart() =>
        _heart ??= PixelSprite.Create(
            Heart,
            new Dictionary<char, Color> { ['K'] = Color.FromRgb(0xFF, 0x7B, 0x96) });

    private static string[] Compose(FinPose fin, EyePose eyes, int gaze, bool flat)
    {
        var dome = fin switch
        {
            FinPose.Up => DomeUp,
            FinPose.Droop => DomeDroop,
            _ => DomeMid,
        };

        var rows = new string[Height];

        if (flat)
        {
            // 頭の下 FlatDrop 行はどの耳ビレでもただの側面なので、そこを抜いて頭ごと沈める。
            // 目と足は動かさない。背だけが低くなって、ぺたんと潰れて見える
            var blank = new string(PixelSprite.Transparent, Width);
            for (var y = 0; y < FlatDrop; y++)
            {
                rows[y] = blank;
            }

            Array.Copy(dome, 0, rows, FlatDrop, DomeHeight - FlatDrop);
        }
        else
        {
            Array.Copy(dome, 0, rows, 0, DomeHeight);
        }

        Array.Copy(Body, 0, rows, DomeHeight, Body.Length);

        StampEyes(rows, eyes, gaze);
        return rows;
    }

    private static void StampEyes(string[] rows, EyePose eyes, int gaze)
    {
        // 左目の左上を LeftEyeX / EyeTopY からどれだけずらすか。
        // Open より幅のある目は外側へ広げる。内側へ広げると両目が寄って見える
        var (pattern, offsetX, offsetY) = eyes switch
        {
            EyePose.Closed => (ClosedEye, -1, 2),
            EyePose.Happy => (HappyEye, -1, 1),
            EyePose.Surprised => (SurprisedEye, -2, 0),
            _ => (OpenEye, 0, 0),
        };

        var leftX = LeftEyeX + offsetX;
        var rightX = Width - leftX - pattern[0].Length;
        var top = EyeTopY + offsetY;

        // 視線は折り返さない。両目とも同じ向きへずらす
        var shift = gaze * GazeShift;
        Stamp(rows, pattern, leftX + shift, top, mirror: false);
        Stamp(rows, pattern, rightX + shift, top, mirror: true);
    }

    private static void Stamp(string[] rows, string[] pattern, int left, int top, bool mirror)
    {
        for (var y = 0; y < pattern.Length; y++)
        {
            var line = pattern[y];
            for (var x = 0; x < line.Length; x++)
            {
                var value = mirror ? line[line.Length - 1 - x] : line[x];
                if (value != PixelSprite.Transparent)
                {
                    PixelSprite.Set(rows, left + x, top + y, value);
                }
            }
        }
    }

    /// <summary>育つほど体色が濃くなる。</summary>
    private static Dictionary<char, Color> Palette(GrowthStage stage)
    {
        var (light, body, shade, deep) = stage switch
        {
            GrowthStage.Hatchling => (Rgb(0xF8, 0xA9, 0x9F), Rgb(0xF0, 0x83, 0x7A), Rgb(0xD9, 0x6A, 0x61), Rgb(0xBC, 0x52, 0x4B)),
            GrowthStage.Juvenile => (Rgb(0xF4, 0x92, 0x87), Rgb(0xE9, 0x6A, 0x5E), Rgb(0xCE, 0x54, 0x49), Rgb(0xB0, 0x41, 0x38)),
            GrowthStage.Adult => (Rgb(0xEF, 0x80, 0x75), Rgb(0xE2, 0x57, 0x4B), Rgb(0xC4, 0x48, 0x3E), Rgb(0xA3, 0x36, 0x2E)),
            GrowthStage.Elder => (Rgb(0xDA, 0x67, 0x5E), Rgb(0xC7, 0x44, 0x3C), Rgb(0xA5, 0x34, 0x2E), Rgb(0x86, 0x26, 0x22)),
            _ => (Rgb(0xEF, 0x80, 0x75), Rgb(0xE2, 0x57, 0x4B), Rgb(0xC4, 0x48, 0x3E), Rgb(0xA3, 0x36, 0x2E)),
        };

        return new Dictionary<char, Color>
        {
            ['K'] = Outline,
            ['H'] = light,
            ['R'] = body,
            ['D'] = shade,
            ['E'] = deep,
            ['W'] = Colors.White,
        };
    }

    private static Dictionary<char, Color> EggPalette() => new()
    {
        ['K'] = Outline,
        ['H'] = Rgb(0xFF, 0xF7, 0xE8),
        ['R'] = Rgb(0xF4, 0xE3, 0xC8),
        ['D'] = Rgb(0xDC, 0xC6, 0xA6),
        ['S'] = Rgb(0xD9, 0xBE, 0x9A),
    };

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
}

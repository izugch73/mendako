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
///   .  透明     K  輪郭     R  体     D  影     W  白目     S  たまごの斑点
///
/// 頭 (耳ビレを含む上 8 行) と胴 (下 9 行) を別に持ち、
/// 組み合わせ + 目のスタンプでコマを合成している。全コマを手で書くより差分が追いやすい。
/// </summary>
public static class MendakoSprites
{
    public const int Width = 20;

    public const int Height = 17;

    private const int DomeHeight = 8;

    /// <summary>ぺたんとしたときに低くなるドット数。</summary>
    private const int FlatDrop = 3;

    private const int LeftEyeX = 7;

    private const int RightEyeX = 12;

    private const int EyeTopY = 9;

    // --- 頭 (0-7 行) ---

    // 頭のてっぺんは細く、その両肩から耳ビレが張り出す。
    // 細い天面と広い耳のあいだにできる段差 (ノッチ) が「耳」に見せる肝で、
    // ここをなだらかにすると途端にただの多角形になる。

    private static readonly string[] DomeUp =
    {
        ".......KKKKKK.......",
        "..KKKKRRRRRRRRKKKK..",
        ".KKRRRRRRRRRRRRRRKK.",
        "..KKRRRRRRRRRRRRKK..",
        "...KRRRRRRRRRRRRK...",
        "...KRRRRRRRRRRRRK...",
        "...KRRRRRRRRRRRRK...",
        "...KRRRRRRRRRRRRK...",
    };

    private static readonly string[] DomeMid =
    {
        ".......KKKKKK.......",
        ".....KKRRRRRRKK.....",
        "..KKKKRRRRRRRRKKKK..",
        ".KKRRRRRRRRRRRRRRKK.",
        "..KKRRRRRRRRRRRRKK..",
        "...KRRRRRRRRRRRRK...",
        "...KRRRRRRRRRRRRK...",
        "...KRRRRRRRRRRRRK...",
    };

    private static readonly string[] DomeDroop =
    {
        ".......KKKKKK.......",
        ".....KKRRRRRRKK.....",
        "....KRRRRRRRRRRK....",
        "...KKRRRRRRRRRRKK...",
        "..KKRRRRRRRRRRRRKK..",
        "..KKRRRRRRRRRRRRKK..",
        "...KRRRRRRRRRRRRK...",
        "...KRRRRRRRRRRRRK...",
    };

    // --- 胴 (8-16 行)。目は後からスタンプする ---
    // 耳ビレをはっきり突き出させるため、胴は頭より細い 12 ドット幅にしてある

    private static readonly string[] Body =
    {
        "...KRRRRRRRRRRRRK...",
        "...KRRRRRRRRRRRRK...",
        "...KRRRRRRRRRRRRK...",
        "...KRRRRRRRRRRRRK...",
        "....KRRRRRRRRRRK....",
        "....KDDDDDDDDDDK....",
        ".....KDDDDDDDDK.....",
        ".....KDDKDDKDDK.....",
        "......KK.KK.KK......",
    };

    // --- たまご ---

    private static readonly string[] Egg =
    {
        "........KKKK........",
        ".......KRRRRK.......",
        "......KRRRRRRK......",
        ".....KRRRRRRRRK.....",
        ".....KRRRRRRRRK.....",
        "....KRRRRRRRRRRK....",
        "....KRRRSSRRRRRK....",
        "...KRRRRSSRRRRRRK...",
        "...KRRRRRRRRRRRRK...",
        "...KRRRRRRRRSSRRK...",
        "...KRRRRRRRRSSRRK...",
        "...KRRRRRRRRRRRRK...",
        "...KRSSRRRRRRRRRK...",
        "...KRSSRRRRRRRRRK...",
        "....KRRRRRRRRRRK....",
        ".....KDDDDDDDDK.....",
        "......KKKKKKKK......",
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

    /// <summary>成長段階ごとの 1 ドットの大きさ (DIP)。育つほど大きく見せる。</summary>
    public static int PixelScale(GrowthStage stage) => stage switch
    {
        GrowthStage.Egg => 5,
        GrowthStage.Hatchling => 4,
        GrowthStage.Juvenile => 5,
        GrowthStage.Adult => 6,
        GrowthStage.Elder => 7,
        _ => 6,
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
        switch (eyes)
        {
            case EyePose.Closed:
                // 横一文字
                foreach (var centre in new[] { LeftEyeX, RightEyeX })
                {
                    PixelSprite.Set(rows, centre - 1, EyeTopY + 1, 'K');
                    PixelSprite.Set(rows, centre, EyeTopY + 1, 'K');
                    PixelSprite.Set(rows, centre + 1, EyeTopY + 1, 'K');
                }

                break;

            case EyePose.Happy:
                // ^ のかたち
                foreach (var centre in new[] { LeftEyeX, RightEyeX })
                {
                    PixelSprite.Set(rows, centre, EyeTopY, 'K');
                    PixelSprite.Set(rows, centre - 1, EyeTopY + 1, 'K');
                    PixelSprite.Set(rows, centre + 1, EyeTopY + 1, 'K');
                }

                break;

            case EyePose.Surprised:
                // 2 x 2 のまんまる。顔の中心に対して左右対称になるよう、外側へ太らせる
                foreach (var (inner, outer) in new[] { (LeftEyeX, LeftEyeX - 1), (RightEyeX, RightEyeX + 1) })
                {
                    PixelSprite.Set(rows, inner, EyeTopY, 'W');
                    PixelSprite.Set(rows, outer, EyeTopY, 'W');
                    PixelSprite.Set(rows, inner, EyeTopY + 1, 'W');
                    PixelSprite.Set(rows, outer, EyeTopY + 1, 'W');
                }

                break;

            default:
                foreach (var centre in new[] { LeftEyeX, RightEyeX })
                {
                    PixelSprite.Set(rows, centre + gaze, EyeTopY, 'W');
                    PixelSprite.Set(rows, centre + gaze, EyeTopY + 1, 'W');
                }

                break;
        }
    }

    /// <summary>育つほど体色が濃くなる。</summary>
    private static Dictionary<char, Color> Palette(GrowthStage stage)
    {
        var (body, shade) = stage switch
        {
            GrowthStage.Hatchling => (Color.FromRgb(0xF0, 0x83, 0x7A), Color.FromRgb(0xD9, 0x6A, 0x61)),
            GrowthStage.Juvenile => (Color.FromRgb(0xE9, 0x6A, 0x5E), Color.FromRgb(0xCE, 0x54, 0x49)),
            GrowthStage.Adult => (Color.FromRgb(0xE2, 0x57, 0x4B), Color.FromRgb(0xC4, 0x48, 0x3E)),
            GrowthStage.Elder => (Color.FromRgb(0xC7, 0x44, 0x3C), Color.FromRgb(0xA5, 0x34, 0x2E)),
            _ => (Color.FromRgb(0xE2, 0x57, 0x4B), Color.FromRgb(0xC4, 0x48, 0x3E)),
        };

        return new Dictionary<char, Color>
        {
            ['K'] = Outline,
            ['R'] = body,
            ['D'] = shade,
            ['W'] = Colors.White,
        };
    }

    private static Dictionary<char, Color> EggPalette() => new()
    {
        ['K'] = Outline,
        ['R'] = Color.FromRgb(0xF4, 0xE3, 0xC8),
        ['D'] = Color.FromRgb(0xDC, 0xC6, 0xA6),
        ['S'] = Color.FromRgb(0xD9, 0xBE, 0x9A),
    };
}

using System;
using System.Collections.Generic;
using System.Windows.Media;
using Mendako.Core.Behavior;
using Mendako.Core.Model;

namespace Mendako.App.Sprites;

/// <summary>
/// 本体に重ねる飾りのドット絵。本体と同じく 1 ドット = 1 文字で書き、同じ倍率で描く。
///
///   .  透明     K  輪郭     W  真珠     w  真珠の影     P  花びら     G  葉
///   Y  黄色     O  黄色の影   B  花芯     S  麦わら     R  リボン
///   H  シルクハット            h  シルクハットの光
///
/// 飾りは部品の集まりで、部品ごとに本体のどの点 (<see cref="MendakoSprites.Anchor"/>) に付くかを持つ。
/// 点の位置は耳ビレのコマとぺたんで変わるので、本体の絵が動けば飾りも付いていく。
/// </summary>
public static class AccessorySprites
{
    /// <summary>飾りの層が本体のコマの上端より上に持つ行数。</summary>
    public const int OverhangTop = 4;

    /// <summary>飾りの層が本体のコマの左右それぞれに持つ列数。</summary>
    public const int OverhangSide = 1;

    public const int Width = MendakoSprites.Width + (OverhangSide * 2);

    public const int Height = MendakoSprites.Height + OverhangTop;

    // --- 一輪の花 ---

    private static readonly Part[] Flower =
    {
        new(MendakoSprites.Anchor.LeftFinBase, 2, 2, new[]
        {
            "..K..",
            ".KPK.",
            "KPYPK",
            "KGPK.",
            ".KK..",
        }),
    };

    // --- ひまわりの冠 ---

    private static readonly Part[] SunflowerCrown =
    {
        new(MendakoSprites.Anchor.Crown, 7, 3, new[]
        {
            ".....KKKK.....",
            "....KOYYOK....",
            ".KKKKYBBYKKKK.",
            "KOYOKYBBYKOYOK",
            "KYBYKOYYOKYBYK",
            "KOYOKKKKKKOYOK",
            "KKKK......KKKK",
        }),
    };

    // --- 麦わら帽子 ---

    private static readonly Part[] StrawHat =
    {
        new(MendakoSprites.Anchor.Crown, 6, 4, new[]
        {
            "...KKKKKK...",
            "..KSSSSSSK..",
            "..KRRRRRRK..",
            ".KSSSSSSSSK.",
            "KSSSSSSSSSSK",
            ".KKKKKKKKKK.",
        }),
    };

    // --- 小さなシルクハット ---

    private static readonly Part[] TopHat =
    {
        new(MendakoSprites.Anchor.Crown, 6, 4, new[]
        {
            ".KKKKKK..",
            ".KhHHHK..",
            "..KhHHHK.",
            "..KRRRRKK",
            "KKKKKKKK.",
        }),
    };

    // --- 真珠のイヤリング ---

    private static readonly string[] Pearl =
    {
        ".K.",
        "KWK",
        "KwK",
        ".K.",
    };

    // 耳ビレの先の真下に下げると、真珠の輪郭が胴の輪郭とつながって耳ビレの一部に見える。
    // 1 列外へずらし、先端から斜めに下がるようにしてある
    private static readonly Part[] PearlEarrings =
    {
        new(MendakoSprites.Anchor.LeftFinTip, 2, -1, Pearl),
        new(MendakoSprites.Anchor.RightFinTip, 0, -1, Pearl),
    };

    // 輪郭は本体と同じ色にしてある。飾りごとの濃い色 (麦わらなら茶) にすると、
    // 本体の輪郭と接する所で線の色が切り替わり、継ぎ目が目立って貼り付けたように見える
    private static readonly Dictionary<char, Color> Palette = new()
    {
        ['K'] = Color.FromRgb(0x1B, 0x24, 0x40),
        ['W'] = Color.FromRgb(0xFA, 0xF7, 0xF0),
        ['w'] = Color.FromRgb(0xC9, 0xC3, 0xD6),
        ['P'] = Color.FromRgb(0xFF, 0xA8, 0xC8),
        ['Y'] = Color.FromRgb(0xFF, 0xCF, 0x3A),
        ['O'] = Color.FromRgb(0xE8, 0x9E, 0x1E),
        ['B'] = Color.FromRgb(0x7A, 0x48, 0x22),
        ['G'] = Color.FromRgb(0x5C, 0xA8, 0x4E),
        ['S'] = Color.FromRgb(0xF2, 0xD3, 0x7C),
        ['R'] = Color.FromRgb(0xE2, 0x2F, 0x3C),
        ['H'] = Color.FromRgb(0x48, 0x4E, 0x6C),
        ['h'] = Color.FromRgb(0x78, 0x82, 0xA8),
    };

    private static readonly Dictionary<(Accessory Accessory, FinPose Fin, bool Flat), MendakoSprites.Frame> Cache = new();

    /// <summary>
    /// 飾りの層。本体のコマの上に <see cref="OverhangTop"/> 行、左右に <see cref="OverhangSide"/> 列足してある。
    /// 本体のコマに重ねるには、この層をその行数・列数ぶん左上へずらして置く。
    /// </summary>
    /// <returns>飾りなし、またはたまごのときは null。</returns>
    public static MendakoSprites.Frame? Get(GrowthStage stage, Accessory accessory, FinPose fin, bool flat)
    {
        if (stage == GrowthStage.Egg || accessory == Accessory.None)
        {
            return null;
        }

        var key = (accessory, fin, flat);
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var rows = Compose(PartsOf(accessory), fin, flat);
        var frame = new MendakoSprites.Frame(PixelSprite.Create(rows, Palette), rows);
        Cache[key] = frame;
        return frame;
    }

    private static Part[] PartsOf(Accessory accessory) => accessory switch
    {
        Accessory.Flower => Flower,
        Accessory.SunflowerCrown => SunflowerCrown,
        Accessory.StrawHat => StrawHat,
        Accessory.TopHat => TopHat,
        Accessory.PearlEarrings => PearlEarrings,
        _ => throw new ArgumentOutOfRangeException(nameof(accessory), accessory, null),
    };

    private static string[] Compose(Part[] parts, FinPose fin, bool flat)
    {
        var rows = new string[Height];
        Array.Fill(rows, new string(PixelSprite.Transparent, Width));

        foreach (var part in parts)
        {
            var (anchorX, anchorY) = MendakoSprites.AnchorOf(part.Anchor, fin, flat);
            var left = anchorX - part.PivotX + OverhangSide;
            var top = anchorY - part.PivotY + OverhangTop;

            // 層の外へ出たドットを黙って捨てると、絵が欠けたまま気付けない
            if (left < 0 || top < 0 || left + part.Rows[0].Length > Width || top + part.Rows.Length > Height)
            {
                throw new InvalidOperationException(
                    $"飾りの部品が層からはみ出しています ({part.Anchor}, {fin}, flat={flat})。");
            }

            for (var y = 0; y < part.Rows.Length; y++)
            {
                var row = part.Rows[y];
                for (var x = 0; x < row.Length; x++)
                {
                    if (row[x] != PixelSprite.Transparent)
                    {
                        PixelSprite.Set(rows, left + x, top + y, row[x]);
                    }
                }
            }
        }

        return rows;
    }

    /// <summary>飾りの部品。絵の (<paramref name="PivotX"/>, <paramref name="PivotY"/>) が本体の <paramref name="Anchor"/> に重なる。</summary>
    private sealed record Part(MendakoSprites.Anchor Anchor, int PivotX, int PivotY, string[] Rows);
}

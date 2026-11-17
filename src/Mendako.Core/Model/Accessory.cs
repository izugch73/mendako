namespace Mendako.Core.Model;

/// <summary>メンダコに着せる飾り。</summary>
public enum Accessory
{
    /// <summary>飾りなし。</summary>
    None = 0,

    /// <summary>耳ビレの付け根に挿した一輪の花。</summary>
    Flower = 1,

    /// <summary>頭頂を囲むひまわりの冠。</summary>
    SunflowerCrown = 2,

    /// <summary>赤いリボンの麦わら帽子。</summary>
    StrawHat = 3,

    /// <summary>傾けて載せた小さなシルクハット。</summary>
    TopHat = 4,

    /// <summary>両耳ビレの先に下げた真珠のイヤリング。</summary>
    PearlEarrings = 5,
}

public static class Accessories
{
    public static string DisplayName(Accessory accessory) => accessory switch
    {
        Accessory.None => "なし",
        Accessory.Flower => "いちりんの花",
        Accessory.SunflowerCrown => "ひまわりのかんむり",
        Accessory.StrawHat => "むぎわらぼうし",
        Accessory.TopHat => "ちいさなシルクハット",
        Accessory.PearlEarrings => "しんじゅのイヤリング",
        _ => accessory.ToString(),
    };
}

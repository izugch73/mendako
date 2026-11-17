using Mendako.Core.Model;

namespace Mendako.App.Views;

/// <summary>飾りの表示名。</summary>
public static class AccessoryNames
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

using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mendako.Core.Model;

namespace Mendako.App.Views;

/// <summary>
/// アイテムのアイコン。画像は <see cref="ItemId"/> の名前のファイル (Assets/Items/RedPotion.png など) で、
/// アセンブリに Resource として埋め込んである。単一ファイルで発行しても、別のファイルを探しに行かない。
/// </summary>
public static class ItemIcons
{
    private static readonly Dictionary<ItemId, ImageSource> Cache = new();

    public static ImageSource Get(ItemId id)
    {
        if (Cache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var image = new BitmapImage(new Uri($"pack://application:,,,/Assets/Items/{id}.png", UriKind.Absolute));

        // 表示のたびに読み直さないよう、共有できる形に固める
        image.Freeze();
        Cache[id] = image;
        return image;
    }
}

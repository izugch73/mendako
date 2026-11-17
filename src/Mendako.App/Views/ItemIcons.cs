using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mendako.Core.Model;

namespace Mendako.App.Views;

/// <summary>
/// アイテムのアイコン。画像は <see cref="ItemId"/> の名前のファイル (Assets/Items/RedPotion.png など) で、
/// アセンブリに Resource として埋め込んである。単一ファイルで発行しても、別のファイルを探しに行かない。
/// アセンブリ名を明示して引くので、確認用ツール (SpritePreview) のように別の exe から読んでも同じ絵が出る。
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

        var image = new BitmapImage(new Uri($"pack://application:,,,/Mendako;component/Assets/Items/{id}.png", UriKind.Absolute));
        image.Freeze();
        Cache[id] = image;
        return image;
    }
}

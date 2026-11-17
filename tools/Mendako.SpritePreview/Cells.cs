using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mendako.App.Sprites;
using Mendako.App.Views;
using Mendako.Core.Behavior;
using Mendako.Core.Model;

namespace Mendako.SpritePreview;

/// <summary>一覧に並べるマスの作り方。新しい種類の絵を並べたいときはここに足す。</summary>
internal static class Cells
{
    private static readonly Pen BoundsPen = CreatePen(Color.FromArgb(0x60, 0x50, 0x60, 0x80));

    /// <summary>ビットマップを <paramref name="scale"/> 倍で置く。外周の細線はビットマップの範囲。</summary>
    public static PreviewCell Bitmap(string label, BitmapSource bitmap, int scale)
    {
        var scaled = Pixels.Upscale(bitmap, scale);
        var width = scaled.PixelWidth;
        var height = scaled.PixelHeight;

        return new PreviewCell(label, width + 2, height + 2, dc =>
        {
            dc.DrawRectangle(null, BoundsPen, new Rect(0.5, 0.5, width + 1, height + 1));
            dc.DrawImage(scaled, new Rect(1, 1, width, height));
        });
    }

    /// <summary>
    /// 本体のコマに飾りの層を重ねて <paramref name="scale"/> 倍で置く。外周の細線は飾りの層の範囲、
    /// 内側の細線は本体のコマの範囲。
    /// </summary>
    public static PreviewCell Dressed(string label, BitmapSource body, BitmapSource accessory, int scale)
    {
        var scaledBody = Pixels.Upscale(body, scale);
        var scaledAccessory = Pixels.Upscale(accessory, scale);
        var width = scaledAccessory.PixelWidth;
        var height = scaledAccessory.PixelHeight;
        var bodyLeft = AccessorySprites.OverhangSide * scale;
        var bodyTop = AccessorySprites.OverhangTop * scale;
        var bodyWidth = scaledBody.PixelWidth;
        var bodyHeight = scaledBody.PixelHeight;

        return new PreviewCell(label, width + 2, height + 2, dc =>
        {
            dc.DrawRectangle(null, BoundsPen, new Rect(0.5, 0.5, width + 1, height + 1));
            dc.DrawRectangle(null, BoundsPen, new Rect(bodyLeft + 0.5, bodyTop + 0.5, bodyWidth + 1, bodyHeight + 1));
            dc.DrawImage(scaledBody, new Rect(bodyLeft + 1, bodyTop + 1, bodyWidth, bodyHeight));
            dc.DrawImage(scaledAccessory, new Rect(1, 1, width, height));
        });
    }

    /// <summary>
    /// アプリと同じ <see cref="MendakoVisual"/> に <paramref name="pose"/> を当てて描く。
    /// 吹き出しや揺れのように、コマの外で足される見た目まで確認できる。
    /// </summary>
    public static PreviewCell Visual(string label, PetPose pose, MendakoState state, Accessory accessory = Accessory.None)
    {
        var visual = new MendakoVisual { Accessory = accessory };
        visual.Apply(pose, state);

        var size = new Size(visual.Width, visual.Height);
        visual.Measure(size);
        visual.Arrange(new Rect(size));
        visual.UpdateLayout();

        var width = (int)size.Width;
        var height = (int)size.Height;
        var bitmap = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();

        return new PreviewCell(label, width + 2, height + 2, dc =>
        {
            dc.DrawRectangle(null, BoundsPen, new Rect(0.5, 0.5, width + 1, height + 1));
            dc.DrawImage(bitmap, new Rect(1, 1, width, height));
        });
    }

    /// <summary>その段階にちょうど達した、機嫌のよい状態。</summary>
    public static MendakoState StateAt(GrowthStage stage, bool asleep = false) =>
        MendakoState.CreateNew(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero)) with
        {
            Growth = GrowthStages.Thresholds[(int)stage],
            Satiety = 80d,
            Energy = 80d,
            Affection = 80d,
            IsAsleep = asleep,
        };

    private static Pen CreatePen(Color color)
    {
        var pen = new Pen(new SolidColorBrush(color), 1d);
        pen.Freeze();
        return pen;
    }
}

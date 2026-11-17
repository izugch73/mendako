using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Mendako.App.Services;
using Mendako.App.Sprites;
using Mendako.App.Views;
using Mendako.Core.Behavior;
using Mendako.Core.Model;

namespace Mendako.SpritePreview;

/// <summary>
/// 遊び方ページ (docs/) に載せる画像を書き出す。アプリは起動せず、画面も撮らない。
/// ドット絵は <see cref="MendakoVisual"/> を、窓の中身は <see cref="InventoryWindow"/> と
/// <see cref="ShopWindow"/> の本物のコントロールを、テスト用の状態で RenderTargetBitmap に描く。
/// 保存データ (%LOCALAPPDATA%\Mendako) には触れない。
/// </summary>
internal static class SiteImages
{
    /// <summary>画像は 2 倍の密度で書き出す。ページでは 1 ドットがちょうど整数ピクセルになる大きさで見せる。</summary>
    private const int Density = 2;

    private const double Dpi = 96d * Density;

    /// <summary>MendakoVisual の枠。足元は枠の下端から 18 DIP 上。</summary>
    private const double VisualWidth = 184d;

    private const double VisualHeight = 186d;

    private const double VisualFootY = 168d;

    /// <summary>PetWindow の高さと、足元をタスクバーに沈める量。ホバーのカードの位置合わせに使う。</summary>
    private const double PetWindowHeight = 270d;

    private const double SinkIntoTaskbar = 16d;

    public static int Run(string outputDirectory)
    {
        var directory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(directory);

        // アプリの配色 (Theme.xaml) を、このツールからも引けるようにする
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Mendako;component/Views/Theme.xaml", UriKind.Absolute),
        });

        var written = new List<string>();

        void Save(string name, BitmapSource bitmap)
        {
            PreviewSheet.SavePng(bitmap, Path.Combine(directory, name));
            written.Add($"{name} ({bitmap.PixelWidth}x{bitmap.PixelHeight})");
        }

        // (a) 成長 5 段階。同じ切り出しにそろえて、段階ごとの大きさの違いがそのまま見えるようにする
        var stages = Enum.GetValues<GrowthStage>();
        var stageFrames = stages
            .Select(stage => (Stage: stage, Bitmap: RenderPose(stage, PetPose.Neutral, Accessory.None)))
            .ToList();
        foreach (var (stage, bitmap) in CropTogether(stageFrames.Select(f => f.Bitmap).ToList(), 6)
            .Select((bitmap, i) => (stageFrames[i].Stage, bitmap)))
        {
            Save($"stage-{stage.ToString().ToLowerInvariant()}.png", bitmap);
        }

        // (b) 飾り 5 種。おみせの見本と同じ Adult
        var accessories = Enum.GetValues<Accessory>().Where(a => a != Accessory.None).ToList();
        var dressed = accessories
            .Select(a => RenderPose(GrowthStage.Adult, PetPose.Neutral, a))
            .ToList();
        foreach (var (accessory, bitmap) in CropTogether(dressed, 6).Select((bitmap, i) => (accessories[i], bitmap)))
        {
            Save($"accessory-{accessory.ToString().ToLowerInvariant()}.png", bitmap);
        }

        // しぐさ。操作の表とごはん・なでる・寝るの説明に使う
        var happy = new PetPose { Fin = FinPose.Up, Eyes = EyePose.Happy, BobDots = -2d, ShowHeart = true };
        var poses = new (string Name, PetPose Pose)[]
        {
            ("happy", happy),
            ("eat", new PetPose { Fin = FinPose.Up, Eyes = EyePose.Closed, BobDots = -1d }),
            ("sleep", new PetPose { Fin = FinPose.Droop, Eyes = EyePose.Closed, ShowSleepMark = true }),
            ("startle", new PetPose { Fin = FinPose.Up, Eyes = EyePose.Surprised, BobDots = -3d }),
            ("flat", new PetPose { Fin = FinPose.Droop, Eyes = EyePose.Open, Flat = true }),
        };
        var posed = poses.Select(p => RenderPose(GrowthStage.Adult, p.Pose, Accessory.None)).ToList();
        foreach (var (name, bitmap) in CropTogether(posed, 6).Select((bitmap, i) => (poses[i].Name, bitmap)))
        {
            Save($"pose-{name}.png", bitmap);
        }

        // 見出しの絵。ハートつきで少し大きく見せる
        Save("hero.png", CropTogether(new[] { RenderPose(GrowthStage.Adult, happy, Accessory.None) }, 6)[0]);

        // (c) タスクバー風の帯の上のメンダコと置いたアイテム
        var open = PetPose.Neutral;
        Save("scene-all.png", Scene(GrowthStage.Adult, open, Accessory.None, 170d, new[]
        {
            (ItemId.Apple, 72d),
            (ItemId.RedFlower, 272d),
            (ItemId.Diamond, 308d),
        }));
        Save("scene-food.png", Scene(GrowthStage.Adult, poses[1].Pose, Accessory.None, 190d, new[] { (ItemId.Apple, 108d) }));
        Save("scene-material.png", Scene(GrowthStage.Adult, happy, Accessory.None, 190d, new[] { (ItemId.RedFlower, 108d) }));
        Save("scene-treasure.png", Scene(
            GrowthStage.Adult,
            new PetPose { Fin = FinPose.Up, Eyes = EyePose.Happy, BobDots = -1d, ShowHeart = true },
            Accessory.None,
            190d,
            new[] { (ItemId.Diamond, 108d) }));
        Save("scene-gear.png", Scene(GrowthStage.Adult, poses[3].Pose, Accessory.None, 190d, new[] { (ItemId.RedPotion, 108d) }));

        // ホバーで出るステータスカード。本物の PetWindow から取り出して、メンダコと同じ場面に置く
        var hoverState = Cells.StateAt(GrowthStage.Adult) with
        {
            Name = "めんちゃん",
            Growth = 15_000d + (0.4d * 30_000d),
            Satiety = 78d,
            Energy = 64d,
            Affection = 57d,
        };
        Save("scene-hover.png", HoverScene(hoverState));

        // (d) もちもの・おみせ。本物の窓の中身を、テスト用の状態で描く
        var temporary = Path.Combine(Path.GetTempPath(), $"mendako-site-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(temporary);
            Save("window-inventory.png", InventoryImage(temporary));
            Save("window-shop.png", ShopImage(temporary));
        }
        finally
        {
            try
            {
                Directory.Delete(temporary, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        Save("favicon.png", Favicon());

        Console.WriteLine($"{directory} に {written.Count} 枚を書きました");
        foreach (var line in written)
        {
            Console.WriteLine($"  {line}");
        }

        application.Shutdown();
        return 0;
    }

    // --- メンダコ本体 ---

    /// <summary><see cref="MendakoVisual"/> を透明な背景のまま 2 倍の密度で描く (枠の大きさそのまま)。</summary>
    private static BitmapSource RenderPose(GrowthStage stage, PetPose pose, Accessory accessory)
    {
        var visual = new MendakoVisual { Accessory = accessory };
        visual.Apply(pose, Cells.StateAt(stage));
        return RenderElement(visual, VisualWidth, VisualHeight, background: null);
    }

    private static BitmapSource RenderElement(FrameworkElement element, double width, double height, Brush? background)
    {
        var host = new Border { Width = width, Height = height, Background = background, Child = element };
        host.UseLayoutRounding = true;
        host.SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(host, TextFormattingMode.Display);

        var size = new Size(width, height);
        host.Measure(size);
        host.Arrange(new Rect(size));
        host.UpdateLayout();

        // ListBox の行などは Loaded より後に実体が作られる。描く前に一度流しておく
        host.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
        host.UpdateLayout();

        var bitmap = new RenderTargetBitmap((int)(width * Density), (int)(height * Density), Dpi, Dpi, PixelFormats.Pbgra32);
        bitmap.Render(host);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// 絵のある範囲 (全部の和) で同じ大きさに切り出す。透明な余白だけを削るので、足元の位置と大きさの差は保たれる。
    /// </summary>
    private static IReadOnlyList<BitmapSource> CropTogether(IReadOnlyList<BitmapSource> frames, int padding)
    {
        var left = int.MaxValue;
        var top = int.MaxValue;
        var right = int.MinValue;
        var bottom = int.MinValue;

        foreach (var frame in frames)
        {
            var width = frame.PixelWidth;
            var height = frame.PixelHeight;
            var pixels = new byte[width * height * 4];
            frame.CopyPixels(pixels, width * 4, 0);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (pixels[(((y * width) + x) * 4) + 3] == 0)
                    {
                        continue;
                    }

                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }
        }

        var first = frames[0];
        left = Math.Max(0, left - padding);
        top = Math.Max(0, top - padding);
        right = Math.Min(first.PixelWidth - 1, right + padding);
        bottom = Math.Min(first.PixelHeight - 1, bottom + padding);

        // ページでは半分の大きさで見せるので、幅も高さも偶数にそろえる
        if ((right - left + 1) % 2 != 0)
        {
            right = right + 1 < first.PixelWidth ? right + 1 : right;
            left = (right - left + 1) % 2 != 0 ? left - 1 : left;
        }

        if ((bottom - top + 1) % 2 != 0)
        {
            bottom = bottom + 1 < first.PixelHeight ? bottom + 1 : bottom;
            top = (bottom - top + 1) % 2 != 0 ? top - 1 : top;
        }

        var rect = new Int32Rect(left, top, right - left + 1, bottom - top + 1);
        return frames
            .Select(frame =>
            {
                var cropped = new CroppedBitmap(frame, rect);
                cropped.Freeze();
                return (BitmapSource)cropped;
            })
            .ToList();
    }

    // --- 場面 ---

    private const double SceneWidth = 340d;

    /// <summary>タスクバー風の帯の上に、メンダコと置いたアイテムを並べる。画面の撮影ではなく描画で作る。</summary>
    private static BitmapSource Scene(
        GrowthStage stage,
        PetPose pose,
        Accessory accessory,
        double petCentre,
        IReadOnlyList<(ItemId Item, double Centre)> items)
    {
        const double BarTop = 150d;
        return DrawScene(SceneWidth, BarTop, 40d, dc =>
        {
            DrawPet(dc, stage, pose, accessory, petCentre, BarTop);
            foreach (var (item, centre) in items)
            {
                DrawItem(dc, item, centre, BarTop);
            }
        });
    }

    private static BitmapSource DrawScene(double width, double barTop, double barHeight, Action<DrawingContext> contents)
    {
        var height = barTop + barHeight;
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        RenderOptions.SetEdgeMode(visual, EdgeMode.Aliased);

        using (var dc = visual.RenderOpen())
        {
            // 深い海のような壁紙。上が濃く、帯に近づくと明るい
            var sky = new LinearGradientBrush(
                Color.FromRgb(0x17, 0x27, 0x42),
                Color.FromRgb(0x2E, 0x5B, 0x78),
                90d);
            sky.Freeze();
            dc.DrawRectangle(sky, null, new Rect(0, 0, width, barTop));

            // 泡。ドット絵の雰囲気に合わせて四角いドットにする
            var bubble = Frozen(new SolidColorBrush(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF)));
            foreach (var (x, y, size) in new (double, double, double)[]
            {
                (24, 20, 4), (31, 34, 2), (300, 14, 4), (312, 30, 2), (230, 26, 2), (96, 12, 2), (262, 52, 4),
            })
            {
                dc.DrawRectangle(bubble, null, new Rect(x, y, size, size));
            }

            // タスクバー。中身は特定のアプリに見えない、ただの四角にする
            dc.DrawRectangle(Frozen(new SolidColorBrush(Color.FromRgb(0x1E, 0x21, 0x28))), null, new Rect(0, barTop, width, barHeight));
            dc.DrawRectangle(Frozen(new SolidColorBrush(Color.FromRgb(0x33, 0x38, 0x42))), null, new Rect(0, barTop, width, 1));

            var icon = Frozen(new SolidColorBrush(Color.FromRgb(0x4B, 0x55, 0x66)));
            var iconAccent = Frozen(new SolidColorBrush(Color.FromRgb(0x6C, 0x7A, 0x90)));
            var startX = (width / 2d) - 70d;
            for (var i = 0; i < 5; i++)
            {
                var rect = new Rect(startX + (i * 30d), barTop + 9d, 22d, 22d);
                dc.DrawRoundedRectangle(i == 1 ? iconAccent : icon, null, rect, 4d, 4d);
            }

            var clock = Frozen(new SolidColorBrush(Color.FromRgb(0x5A, 0x64, 0x76)));
            dc.DrawRectangle(clock, null, new Rect(width - 48d, barTop + 12d, 36d, 4d));
            dc.DrawRectangle(clock, null, new Rect(width - 42d, barTop + 22d, 30d, 4d));

            contents(dc);
        }

        var bitmap = new RenderTargetBitmap((int)(width * Density), (int)(height * Density), Dpi, Dpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// メンダコを帯の上に立たせる。PetWindow と同じく、枠の下端を帯の上端より 16 DIP 下に置く。
    /// 足元は枠の下端から 18 DIP 上なので、帯の上端のすぐ上に立つ。
    /// </summary>
    private static void DrawPet(DrawingContext dc, GrowthStage stage, PetPose pose, Accessory accessory, double centre, double barTop)
    {
        var bitmap = RenderPose(stage, pose, accessory);
        var top = barTop + SinkIntoTaskbar - VisualHeight;
        dc.DrawImage(bitmap, new Rect(centre - (VisualWidth / 2d), top, VisualWidth, VisualHeight));
    }

    /// <summary>置いたアイテム。縮小アイコン (18 ドット) を、アプリと同じ 1 ドット 2 DIP で、メンダコの足元に合わせる。</summary>
    private static void DrawItem(DrawingContext dc, ItemId item, double centre, double barTop)
    {
        var art = ScatterIcons.Get(item);
        var size = ScatterItemWindow.SizeDips;
        var bitmap = Pixels.Upscale(art.Bitmap, (int)(size * Density / ScatterIcons.Dots));
        var ground = barTop + SinkIntoTaskbar - (VisualHeight - VisualFootY);
        var padding = art.BottomPaddingDots * size / ScatterIcons.Dots;
        dc.DrawImage(bitmap, new Rect(centre - (size / 2d), ground - size + padding, size, size));
    }

    /// <summary>ホバーで出るステータスカードを、本物の PetWindow から取り出して、メンダコの頭の上に重ねる。</summary>
    private static BitmapSource HoverScene(MendakoState state)
    {
        var window = new PetWindow();
        window.Initialize(state, new AppSettings());

        var card = (Border)window.FindName("StatusCard");
        ((Panel)card.Parent).Children.Remove(card);
        card.Visibility = Visibility.Visible;
        card.Margin = new Thickness(0);

        const double Shadow = 18d;
        var cardImage = RenderElement(card, card.Width + (Shadow * 2d), 260d, background: null, inset: Shadow);

        // 窓の上端から数えた位置にそろえる。足元は枠の下端から 18 DIP 上。
        // 実機ではカードが頭の上端に少し重なる (README の既知の制限)。ページでは頭が見えるよう、メンダコを少し下げて重ならない位置にする
        const double Lift = 14d;
        var barTop = PetWindowHeight - SinkIntoTaskbar + Lift;
        var petCentre = SceneWidth / 2d;

        return DrawScene(SceneWidth, barTop, 40d, dc =>
        {
            DrawPet(dc, GrowthStage.Adult, PetPose.Neutral, Accessory.None, petCentre, barTop);
            dc.DrawImage(cardImage, new Rect(petCentre - ((card.Width + (Shadow * 2d)) / 2d), 4d - Shadow, card.Width + (Shadow * 2d), 260d));
        });
    }

    private static BitmapSource RenderElement(FrameworkElement element, double width, double height, Brush? background, double inset)
    {
        var host = new Grid { Width = width, Height = height, Background = background };
        element.HorizontalAlignment = HorizontalAlignment.Center;
        element.VerticalAlignment = VerticalAlignment.Top;
        element.Margin = new Thickness(0, inset, 0, 0);
        host.Children.Add(element);

        var size = new Size(width, height);
        host.Measure(size);
        host.Arrange(new Rect(size));
        host.UpdateLayout();

        var bitmap = new RenderTargetBitmap((int)(width * Density), (int)(height * Density), Dpi, Dpi, PixelFormats.Pbgra32);
        bitmap.Render(host);
        bitmap.Freeze();
        return bitmap;
    }

    // --- 窓 ---

    private static BitmapSource InventoryImage(string temporary)
    {
        var state = Cells.StateAt(GrowthStage.Adult) with
        {
            Shells = 235,
            Items = new[]
            {
                ItemId.Bread, ItemId.Apple, ItemId.Apple, ItemId.Apple, ItemId.Fish, ItemId.Cake,
                ItemId.RedFlower, ItemId.RedFlower, ItemId.Clover, ItemId.RedPotion, ItemId.Diamond,
            },
            ScatteredItems = new[]
            {
                new ScatteredItem { Id = Guid.NewGuid(), Item = ItemId.Mushroom, Position = 0.4d },
            },
        };

        using var session = NewSession(temporary, state);
        var window = new InventoryWindow(session, () => null);

        var list = (ListBox)window.FindName("ItemList");
        var width = window.Width;
        var height = window.Height;
        var content = (FrameworkElement)window.Content;
        window.Content = null;

        // 行ごとに「うる」を押す前の、選んだだけの状態にしておく
        list.SelectedIndex = 0;
        return RenderWindowContent(content, width, height);
    }

    private static BitmapSource ShopImage(string temporary)
    {
        var state = Cells.StateAt(GrowthStage.Adult) with
        {
            Shells = 270,
            OwnedAccessories = new[] { Accessory.Flower, Accessory.StrawHat },
            EquippedAccessory = Accessory.Flower,
        };

        using var session = NewSession(temporary, state);
        var window = new ShopWindow(session);

        var width = window.Width;
        var height = window.Height;
        var content = (FrameworkElement)window.Content;
        window.Content = null;
        return RenderWindowContent(content, width, height);
    }

    private static MendakoSession NewSession(string temporary, MendakoState state)
    {
        // 保存先は一時フォルダ。自分の保存データ (state.json) は読みも書きもしない
        var store = new JsonFileStore<MendakoState>(Path.Combine(temporary, $"{Guid.NewGuid():N}.json"));
        store.Save(state);
        return new MendakoSession(store: store);
    }

    private static BitmapSource RenderWindowContent(FrameworkElement content, double width, double height)
    {
        var background = (Brush)Application.Current.FindResource("WindowBackground");
        TextElement.SetForeground(content, (Brush)Application.Current.FindResource("CardForeground"));
        return RenderElement(content, width, height, background);
    }

    // --- アイコン ---

    private static BitmapSource Favicon()
    {
        const int Size = 40;
        var sprite = Pixels.Upscale(MendakoSprites.Get(GrowthStage.Adult, FinPose.Mid, EyePose.Open).Bitmap, 2);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(sprite, new Rect(0, (Size - sprite.PixelHeight) / 2d, sprite.PixelWidth, sprite.PixelHeight));
        }

        var bitmap = new RenderTargetBitmap(Size, Size, 96d, 96d, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static T Frozen<T>(T freezable)
        where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}

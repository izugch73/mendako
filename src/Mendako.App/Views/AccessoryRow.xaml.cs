using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Mendako.App.Sprites;
using Mendako.Core.Behavior;
using Mendako.Core.Model;

namespace Mendako.App.Views;

/// <summary>
/// おみせの 1 行。飾りを付けたメンダコの絵と値段、買う・付ける・外すのボタンを持つ。
/// 行は作り直さず、状態が変わるたびに <see cref="Update"/> で書き換える。
/// ボタンを押した直後に作り直すとキーボードのフォーカスが消え、続けて操作できなくなる。
/// </summary>
public partial class AccessoryRow : UserControl
{
    /// <summary>プレビューの 1 ドットの大きさ (DIP)。</summary>
    private const int PreviewPixelScale = 3;

    private AccessoryAction _action = AccessoryAction.Buy;

    private readonly int _price;

    public AccessoryRow(Accessory accessory, int price)
    {
        Accessory = accessory;
        _price = price;

        InitializeComponent();

        NameText.Text = Accessories.DisplayName(accessory);
        BuildPreview();
    }

    /// <summary>ボタンの働き。押したときにどの操作を頼むかを行が決める。</summary>
    public enum AccessoryAction
    {
        Buy,
        Equip,
        Unequip,
    }

    public Accessory Accessory { get; }

    /// <summary>ボタンが押された。</summary>
    public event EventHandler<AccessoryAction>? ActionRequested;

    public void Update(MendakoState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var owned = state.OwnedAccessories.Contains(Accessory);
        var equipped = state.EquippedAccessory == Accessory;

        PriceText.Text = $"{_price} かいがら";
        Frame.BorderBrush = equipped ? (Brush)FindResource("AccentPink") : Brushes.Transparent;

        if (equipped)
        {
            SetStatus("つけているよ", "AccentPink");
            SetAction(AccessoryAction.Unequip, "はずす", enabled: true);
        }
        else if (owned)
        {
            SetStatus("もっているよ", "AccentTeal");
            SetAction(AccessoryAction.Equip, "つける", enabled: true);
        }
        else if (state.Shells >= _price)
        {
            SetStatus("かえるよ", "AccentGold");
            SetAction(AccessoryAction.Buy, "かう", enabled: true);
        }
        else
        {
            SetStatus($"あと {_price - state.Shells} かいがら", "CardMuted");
            SetAction(AccessoryAction.Buy, "かう", enabled: false);
        }
    }

    private void SetStatus(string text, string brushKey)
    {
        StatusText.Text = text;
        StatusText.Foreground = (Brush)FindResource(brushKey);
    }

    private void SetAction(AccessoryAction action, string text, bool enabled)
    {
        _action = action;
        ActionButton.Content = text;
        ActionButton.IsEnabled = enabled;
    }

    /// <summary>ぬしの絵に重ねる。飾りを買うのは実質ぬしだけなので、見本も常にぬしで出す。</summary>
    private void BuildPreview()
    {
        var body = MendakoSprites.Get(GrowthStage.Elder, FinPose.Mid, EyePose.Open);
        var layer = AccessorySprites.Get(GrowthStage.Elder, Accessory, FinPose.Mid, flat: false)
            ?? throw new InvalidOperationException($"飾りの絵がありません ({Accessory})。");

        Preview.Width = AccessorySprites.Width * PreviewPixelScale;
        Preview.Height = AccessorySprites.Height * PreviewPixelScale;

        BodyImage.Source = body.Bitmap;
        BodyImage.Width = MendakoSprites.Width * PreviewPixelScale;
        BodyImage.Height = MendakoSprites.Height * PreviewPixelScale;
        Canvas.SetLeft(BodyImage, AccessorySprites.OverhangSide * PreviewPixelScale);
        Canvas.SetTop(BodyImage, AccessorySprites.OverhangTop * PreviewPixelScale);

        AccessoryImage.Source = layer.Bitmap;
        AccessoryImage.Width = AccessorySprites.Width * PreviewPixelScale;
        AccessoryImage.Height = AccessorySprites.Height * PreviewPixelScale;
    }

    private void OnActionClick(object sender, RoutedEventArgs e) => ActionRequested?.Invoke(this, _action);
}

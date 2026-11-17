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

    /// <summary>
    /// 見本に使う最も若い成長段階。飾りの見た目は Adult と Elder でしか確かめていないので、
    /// それより若い段階では Adult の絵で描く。
    /// </summary>
    private const GrowthStage PreviewMinStage = GrowthStage.Adult;

    private AccessoryAction _action = AccessoryAction.Buy;

    private readonly int _price;

    /// <summary>見本を描いている成長段階。</summary>
    private GrowthStage _previewStage = PreviewMinStage;

    public AccessoryRow(Accessory accessory, int price)
    {
        Accessory = accessory;
        _price = price;

        InitializeComponent();

        NameText.Text = Accessories.DisplayName(accessory);
        BuildPreview(_previewStage);
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

        var stage = state.Stage > PreviewMinStage ? state.Stage : PreviewMinStage;
        if (stage != _previewStage)
        {
            _previewStage = stage;
            BuildPreview(stage);
        }

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

    /// <summary>いまの成長段階の絵に重ねる。実際に付けたときと同じ姿を見せるため。</summary>
    private void BuildPreview(GrowthStage stage)
    {
        var body = MendakoSprites.Get(stage, FinPose.Mid, EyePose.Open);
        var layer = AccessorySprites.Get(stage, Accessory, FinPose.Mid, flat: false)
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

using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Mendako.Core.Model;
using Mendako.Platform;

namespace Mendako.App.Views;

/// <summary>
/// タスクバーに置いたアイテム 1 個ぶんの透過オーバーレイ。
/// メンダコの窓 (260×270) の外にも置くので、アイテムごとに絵と同じ大きさの窓を持つ。
/// 位置決めと当たり判定は <see cref="ScatterLayer"/> が行い、ここは絵を出してクリックを知らせるだけ。
/// </summary>
public partial class ScatterItemWindow : Window
{
    /// <summary>窓の 1 辺 (DIP)。<see cref="ScatterIcons.Dots"/> の整数倍にして、ドットを滲ませない。</summary>
    public const double SizeDips = ScatterIcons.Dots * 2d;

    private static readonly Duration FadeDuration = new(TimeSpan.FromMilliseconds(260));

    private ScatterIcon _icon;
    private bool _clickThrough = true;
    private bool _closing;

    public ScatterItemWindow(ScatteredItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Item = item;
        _icon = ScatterIcons.Get(item.Item);

        InitializeComponent();
        ItemImage.Source = _icon.Bitmap;
        Opacity = 0d;
    }

    public ScatteredItem Item { get; set; }

    public IntPtr Handle { get; private set; }

    /// <summary>いまいるモニタの倍率。DPI の違うモニタへ動かすと WPF が更新する。</summary>
    public DpiScale Dpi { get; private set; } = new(1d, 1d);

    public ScatterIcon IconArt => _icon;

    /// <summary>倍率が変わった。ウィンドウの実寸が変わったので、置き直す必要がある。</summary>
    public event EventHandler? DpiScaleChanged;

    /// <summary>クリックされた。</summary>
    public event EventHandler? Clicked;

    /// <summary>
    /// ハンドルを作って、オーバーレイとしてのスタイルを付ける。
    /// Show する前に位置を決められるよう、表示とは分けてある (別のモニタへ置くとき、原点に一瞬出るのを避ける)。
    /// </summary>
    public void Prepare()
    {
        Handle = new WindowInteropHelper(this).EnsureHandle();
        OverlayWindow.ApplyOverlayStyles(Handle);
        OverlayWindow.SetClickThrough(Handle, true);
        Dpi = VisualTreeHelper.GetDpi(this);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Dpi = newDpi;
        DpiScaleChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ShowFaded()
    {
        if (_closing)
        {
            return;
        }

        Show();
        OverlayWindow.ApplyOverlayStyles(Handle);
        OverlayWindow.SetClickThrough(Handle, _clickThrough);
        OverlayWindow.EnsureTopmost(Handle);
        BeginAnimation(OpacityProperty, new DoubleAnimation(1d, FadeDuration));
    }

    /// <summary>消えるときは、ぱっと無くならないように薄くしてから閉じる。</summary>
    public void FadeOutAndClose()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;

        // 消えかけのものをクリックして、片付けた扱いにならないようにする
        OverlayWindow.SetClickThrough(Handle, true);

        var fade = new DoubleAnimation(0d, FadeDuration);
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>画面上の位置 (物理ピクセル) にある絵のドットが不透明か。</summary>
    public bool IsSolidAt(int screenX, int screenY)
    {
        if (OverlayWindow.GetBounds(Handle) is not { } bounds || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        // 負の値は 0 に丸められて隣のドットに当たってしまうので、窓の外は先に弾く
        if (!bounds.Contains(screenX, screenY))
        {
            return false;
        }

        var x = (screenX - bounds.Left) * ScatterIcons.Dots / bounds.Width;
        var y = (screenY - bounds.Top) * ScatterIcons.Dots / bounds.Height;
        return _icon.IsSolid(x, y);
    }

    public void SetClickThrough(bool enabled)
    {
        if (_clickThrough == enabled)
        {
            return;
        }

        _clickThrough = enabled;
        OverlayWindow.SetClickThrough(Handle, enabled);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (!_closing)
        {
            Clicked?.Invoke(this, EventArgs.Empty);
        }

        e.Handled = true;
    }
}

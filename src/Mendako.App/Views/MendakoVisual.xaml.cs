using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Mendako.App.Behavior;
using Mendako.App.Sprites;
using Mendako.Core.Model;

namespace Mendako.App.Views;

/// <summary>
/// メンダコの見た目。<see cref="PetPose"/> のコマ指定をビットマップに置き換えるだけで、
/// アニメーションの判断は一切しない。
/// </summary>
public partial class MendakoVisual : UserControl
{
    /// <summary>スプライトの足元を置く Y 座標。ここを固定するとタスクバーへの接地が段階によらず揃う。</summary>
    private const double FootY = 132d;

    private IReadOnlyList<string> _currentRows = Array.Empty<string>();
    private double _spriteLeft;
    private double _spriteTop;
    private int _pixelScale = 6;

    public MendakoVisual()
    {
        InitializeComponent();
    }

    /// <summary>1 フレーム分の見た目を反映する。</summary>
    public void Apply(PetPose pose, MendakoState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var frame = MendakoSprites.Get(state.Stage, pose.Fin, pose.Eyes);
        _currentRows = frame.Rows;
        _pixelScale = MendakoSprites.PixelScale(state.Stage);

        var spriteWidth = MendakoSprites.Width * _pixelScale;
        var spriteHeight = MendakoSprites.Height * _pixelScale;

        // ドット単位のオフセットを整数に丸めてから実寸へ。半端な位置に置くとドットが滲む
        var bob = Math.Round(pose.BobDots) * _pixelScale;
        var drift = Math.Round(pose.DriftDots) * _pixelScale;

        _spriteLeft = Math.Round((Width - spriteWidth) / 2d) + drift;
        _spriteTop = FootY - spriteHeight + bob;

        BodyImage.Source = frame.Bitmap;
        BodyImage.Width = spriteWidth;
        BodyImage.Height = spriteHeight;
        Canvas.SetLeft(BodyImage, _spriteLeft);
        Canvas.SetTop(BodyImage, _spriteTop);

        UpdateSleepMark(pose.ShowSleepMark, spriteWidth);
        UpdateHeart(pose.ShowHeart, spriteWidth);
    }

    /// <summary>
    /// カーソルがメンダコの絵の上にあるか。透明ドットは外として扱う。
    /// </summary>
    /// <param name="point">このコントロールの座標系での位置。</param>
    public bool HitTestSprite(Point point)
    {
        if (_currentRows.Count == 0 || _pixelScale <= 0)
        {
            return false;
        }

        var x = (int)Math.Floor((point.X - _spriteLeft) / _pixelScale);
        var y = (int)Math.Floor((point.Y - _spriteTop) / _pixelScale);

        if (y < 0 || y >= _currentRows.Count)
        {
            return false;
        }

        var row = _currentRows[y];
        if (x < 0 || x >= row.Length)
        {
            return false;
        }

        return row[x] != PixelSprite.Transparent;
    }

    private void UpdateSleepMark(bool show, double spriteWidth)
    {
        if (!show)
        {
            SleepMarkImage.Visibility = Visibility.Collapsed;
            return;
        }

        var scale = Math.Max(2, _pixelScale - 2);
        SleepMarkImage.Source = MendakoSprites.GetSleepMark();
        SleepMarkImage.Width = 3 * scale;
        SleepMarkImage.Height = 3 * scale;
        SleepMarkImage.Visibility = Visibility.Visible;
        Canvas.SetLeft(SleepMarkImage, _spriteLeft + spriteWidth - (2 * scale));
        Canvas.SetTop(SleepMarkImage, _spriteTop - (4 * scale));
    }

    private void UpdateHeart(bool show, double spriteWidth)
    {
        if (!show)
        {
            HeartImage.Visibility = Visibility.Collapsed;
            return;
        }

        var scale = Math.Max(2, _pixelScale - 2);
        HeartImage.Source = MendakoSprites.GetHeart();
        HeartImage.Width = 5 * scale;
        HeartImage.Height = 5 * scale;
        HeartImage.Visibility = Visibility.Visible;
        Canvas.SetLeft(HeartImage, _spriteLeft + spriteWidth - (3 * scale));
        Canvas.SetTop(HeartImage, _spriteTop - (5 * scale));
    }
}

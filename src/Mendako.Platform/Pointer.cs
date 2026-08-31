namespace Mendako.Platform;

/// <summary>
/// マウスカーソルの位置 (物理ピクセル)。
/// クリックスルーが有効なあいだ WPF はマウスイベントを受け取れないので、
/// あたり判定はこの値をポーリングして行う。
/// </summary>
public static class Pointer
{
    /// <summary>カーソルの現在位置。取得に失敗したら null。</summary>
    public static (int X, int Y)? TryGetPosition()
    {
        if (!NativeMethods.GetCursorPos(out var point))
        {
            return null;
        }

        return (point.X, point.Y);
    }
}

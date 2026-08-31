using System.Runtime.InteropServices;

namespace Mendako.Platform;

/// <summary>タスクバーが画面のどの辺にあるか。</summary>
public enum TaskbarEdge
{
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3,
}

/// <summary>タスクバーの位置情報。座標はすべて物理ピクセル。</summary>
public sealed record TaskbarInfo(
    TaskbarEdge Edge,
    int Left,
    int Top,
    int Right,
    int Bottom,
    bool IsAutoHide)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;
}

/// <summary>
/// タスクバーの位置を取得する。ユーザーがタスクバーを左右上に動かしたり自動的に隠す設定にしても
/// 追随できるよう、Y 座標を決め打ちせず毎回問い合わせる。
/// </summary>
public static class TaskbarLocator
{
    /// <summary>
    /// プライマリモニタのタスクバー情報を返す。取得できなければ null。
    /// </summary>
    /// <remarks>
    /// ABM_GETTASKBARPOS はプライマリのタスクバーしか返さない。
    /// サブモニタのタスクバーに乗せたい場合は Shell_SecondaryTrayWnd を EnumWindows で
    /// 探す必要があるが、v1 では対象外としている。
    /// </remarks>
    public static TaskbarInfo? Locate()
    {
        var data = new NativeMethods.APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(),
        };

        var result = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETTASKBARPOS, ref data);
        if (result != IntPtr.Zero)
        {
            return new TaskbarInfo(
                (TaskbarEdge)data.uEdge,
                data.rc.Left,
                data.rc.Top,
                data.rc.Right,
                data.rc.Bottom,
                IsAutoHide());
        }

        return LocateByWindowHandle();
    }

    /// <summary>タスクバーが「自動的に隠す」設定になっているか。</summary>
    public static bool IsAutoHide()
    {
        var data = new NativeMethods.APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(),
        };

        var state = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETSTATE, ref data).ToInt64();
        return (state & NativeMethods.ABS_AUTOHIDE) != 0;
    }

    /// <summary>ABM_GETTASKBARPOS が失敗したときのフォールバック。</summary>
    private static TaskbarInfo? LocateByWindowHandle()
    {
        var hwnd = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return null;
        }

        // 矩形の縦横比から辺を推測する。横長なら上下、縦長なら左右。
        var edge = rect.Width >= rect.Height
            ? (rect.Top <= 0 ? TaskbarEdge.Top : TaskbarEdge.Bottom)
            : (rect.Left <= 0 ? TaskbarEdge.Left : TaskbarEdge.Right);

        return new TaskbarInfo(edge, rect.Left, rect.Top, rect.Right, rect.Bottom, IsAutoHide());
    }
}

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
/// <param name="MonitorId">
/// 載っているモニタのデバイス名 (<c>\.\DISPLAY2</c> など)。設定に保存してモニタを覚えるのに使う。
/// </param>
/// <param name="MonitorBounds">載っているモニタ全体。</param>
/// <param name="WorkArea">載っているモニタの作業領域。自動的に隠す設定のときの足場になる。</param>
public sealed record TaskbarInfo(
    TaskbarEdge Edge,
    PixelRect Bounds,
    bool IsAutoHide,
    string MonitorId,
    bool IsPrimary,
    PixelRect MonitorBounds,
    PixelRect WorkArea);

/// <summary>
/// タスクバーの位置を取得する。ユーザーがタスクバーを左右上に動かしたり自動的に隠す設定にしても
/// 追随できるよう、Y 座標を決め打ちせず毎回問い合わせる。
/// </summary>
public static class TaskbarLocator
{
    private const string PrimaryClass = "Shell_TrayWnd";

    /// <summary>「タスクバーをすべてのディスプレイに表示する」が有効なときだけ、サブモニタごとに 1 つ存在する。</summary>
    private const string SecondaryClass = "Shell_SecondaryTrayWnd";

    /// <summary>
    /// すべてのモニタのタスクバーを返す。プライマリが取れた場合は必ず先頭。
    /// </summary>
    public static IReadOnlyList<TaskbarInfo> LocateAll()
    {
        var autoHide = IsAutoHide();
        var result = new List<TaskbarInfo>();

        if (LocatePrimary(autoHide) is { } primary)
        {
            result.Add(primary);
        }

        // ABM_GETTASKBARPOS はプライマリしか返さないので、サブモニタの分はウィンドウを直接探す
        var hwnd = IntPtr.Zero;
        while ((hwnd = NativeMethods.FindWindowEx(IntPtr.Zero, hwnd, SecondaryClass, null)) != IntPtr.Zero)
        {
            if (NativeMethods.IsWindowVisible(hwnd) && FromWindow(hwnd, autoHide) is { } secondary)
            {
                result.Add(secondary);
            }
        }

        return result;
    }

    /// <summary>
    /// 指定したモニタのタスクバーを返す。見つからなければプライマリ、それも無ければ null。
    /// モニタを外した・「すべてのディスプレイに表示」を切った、のどちらでもプライマリに戻ってくる。
    /// </summary>
    public static TaskbarInfo? Locate(string? monitorId = null)
    {
        var all = LocateAll();

        if (monitorId is not null)
        {
            foreach (var taskbar in all)
            {
                if (string.Equals(taskbar.MonitorId, monitorId, StringComparison.OrdinalIgnoreCase))
                {
                    return taskbar;
                }
            }
        }

        return all.Count > 0 ? all[0] : null;
    }

    /// <summary>プライマリモニタの作業領域。タスクバーが見つからないときの足場。</summary>
    public static PixelRect? PrimaryWorkArea()
    {
        // 無効なハンドルに DEFAULTTOPRIMARY を付けるとプライマリが返る
        var monitor = NativeMethods.MonitorFromWindow(IntPtr.Zero, NativeMethods.MONITOR_DEFAULTTOPRIMARY);
        return TryGetMonitorInfo(monitor, out var info) ? PixelRect.From(info.rcWork) : null;
    }

    /// <summary>
    /// タスクバーが「自動的に隠す」設定になっているか。Windows 10 / 11 では全モニタ共通の設定。
    /// </summary>
    public static bool IsAutoHide()
    {
        var data = new NativeMethods.APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(),
        };

        var state = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETSTATE, ref data).ToInt64();
        return (state & NativeMethods.ABS_AUTOHIDE) != 0;
    }

    private static TaskbarInfo? LocatePrimary(bool autoHide)
    {
        var data = new NativeMethods.APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(),
        };

        var result = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETTASKBARPOS, ref data);
        if (result != IntPtr.Zero)
        {
            var rect = data.rc;
            var monitor = NativeMethods.MonitorFromRect(ref rect, NativeMethods.MONITOR_DEFAULTTOPRIMARY);
            if (TryGetMonitorInfo(monitor, out var info))
            {
                return Create((TaskbarEdge)data.uEdge, rect, info, autoHide);
            }
        }

        // ABM_GETTASKBARPOS が失敗したときのフォールバック
        var hwnd = NativeMethods.FindWindow(PrimaryClass, null);
        return hwnd == IntPtr.Zero ? null : FromWindow(hwnd, autoHide);
    }

    private static TaskbarInfo? FromWindow(IntPtr hwnd, bool autoHide)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return null;
        }

        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (!TryGetMonitorInfo(monitor, out var info))
        {
            return null;
        }

        return Create(InferEdge(rect, info.rcMonitor), rect, info, autoHide);
    }

    /// <summary>
    /// 矩形の縦横比とモニタ内での寄りから辺を推測する。横長なら上下、縦長なら左右。
    /// 仮想デスクトップの原点ではなくモニタの矩形と比べないと、サブモニタで必ず外す。
    /// </summary>
    private static TaskbarEdge InferEdge(NativeMethods.RECT taskbar, NativeMethods.RECT monitor)
    {
        if (taskbar.Width >= taskbar.Height)
        {
            var distanceToTop = Math.Abs(taskbar.Top - monitor.Top);
            var distanceToBottom = Math.Abs(monitor.Bottom - taskbar.Bottom);
            return distanceToTop < distanceToBottom ? TaskbarEdge.Top : TaskbarEdge.Bottom;
        }

        var distanceToLeft = Math.Abs(taskbar.Left - monitor.Left);
        var distanceToRight = Math.Abs(monitor.Right - taskbar.Right);
        return distanceToLeft < distanceToRight ? TaskbarEdge.Left : TaskbarEdge.Right;
    }

    private static TaskbarInfo Create(
        TaskbarEdge edge,
        NativeMethods.RECT rect,
        NativeMethods.MONITORINFOEX monitor,
        bool autoHide) =>
        new(
            edge,
            PixelRect.From(rect),
            autoHide,
            monitor.szDevice ?? string.Empty,
            (monitor.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0,
            PixelRect.From(monitor.rcMonitor),
            PixelRect.From(monitor.rcWork));

    private static bool TryGetMonitorInfo(IntPtr monitor, out NativeMethods.MONITORINFOEX info)
    {
        info = new NativeMethods.MONITORINFOEX
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEX>(),
        };

        return monitor != IntPtr.Zero && NativeMethods.GetMonitorInfo(monitor, ref info);
    }
}

using System.Runtime.InteropServices;

namespace Mendako.Platform;

/// <summary>ユーザーの状況。オーバーレイを引っ込めるべきかの判断に使う。</summary>
public enum PresenceState
{
    Unknown,

    /// <summary>通常。表示してよい。</summary>
    Normal,

    /// <summary>全画面の D3D アプリ (ゲームなど) が動いている。</summary>
    FullScreenApp,

    /// <summary>プレゼンテーションモード。</summary>
    Presentation,

    /// <summary>全画面アプリ実行中などで通知を出すべきでない。</summary>
    Busy,

    /// <summary>サインイン直後の静かな時間帯。</summary>
    QuietTime,
}

/// <summary>
/// ゲーム中やプレゼン中にメンダコが前面に出てこないようにするための判定。
/// ここを怠ると「プレゼン中に出てきた」で即アンインストールされる。
/// </summary>
public static class UserPresence
{
    public static PresenceState Query()
    {
        if (NativeMethods.SHQueryUserNotificationState(out var raw) != 0)
        {
            return PresenceState.Unknown;
        }

        return raw switch
        {
            NativeMethods.QUNS_BUSY => PresenceState.Busy,
            NativeMethods.QUNS_RUNNING_D3D_FULL_SCREEN => PresenceState.FullScreenApp,
            NativeMethods.QUNS_PRESENTATION_MODE => PresenceState.Presentation,
            NativeMethods.QUNS_QUIET_TIME => PresenceState.QuietTime,
            NativeMethods.QUNS_ACCEPTS_NOTIFICATIONS => PresenceState.Normal,
            NativeMethods.QUNS_APP => PresenceState.Normal,
            NativeMethods.QUNS_NOT_PRESENT => PresenceState.Busy,
            _ => PresenceState.Unknown,
        };
    }

    /// <summary>オーバーレイを隠すべきか。判定できないときは表示側に倒す。</summary>
    /// <param name="overlay">
    /// オーバーレイのウィンドウ。渡すと、全画面アプリが別のモニタにいるときは隠さない。
    /// </param>
    public static bool ShouldHideOverlay(IntPtr overlay = default) => Query() switch
    {
        PresenceState.Presentation => true,
        PresenceState.FullScreenApp or PresenceState.Busy => !IsFullScreenElsewhere(overlay),
        _ => false,
    };

    /// <summary>
    /// 全画面アプリがオーバーレイとは別のモニタにいるか。
    /// SHQueryUserNotificationState は全体でひとつの値しか返さないので、どのモニタの話かは
    /// 最前面のウィンドウから割り出す。割り出せないときは false (= 隠す側) に倒す。
    /// ゲームの上に出てしまうより、いないほうがまし。
    /// </summary>
    private static bool IsFullScreenElsewhere(IntPtr overlay)
    {
        if (overlay == IntPtr.Zero)
        {
            return false;
        }

        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero || !NativeMethods.GetWindowRect(foreground, out var rect))
        {
            return false;
        }

        var monitor = NativeMethods.MonitorFromWindow(foreground, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFOEX
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEX>(),
        };

        if (monitor == IntPtr.Zero || !NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        // 最前面のウィンドウがモニタを覆っていなければ、全画面の持ち主はそれではない
        var screen = info.rcMonitor;
        var covers = rect.Left <= screen.Left
            && rect.Top <= screen.Top
            && rect.Right >= screen.Right
            && rect.Bottom >= screen.Bottom;

        return covers
            && monitor != NativeMethods.MonitorFromWindow(overlay, NativeMethods.MONITOR_DEFAULTTONEAREST);
    }
}

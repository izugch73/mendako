namespace Mendako.Platform;

/// <summary>
/// 常駐オーバーレイとして振る舞うためのウィンドウスタイル操作。
/// このアプリで一番ハマるのがここなので、意図を明示しておく。
/// </summary>
public static class OverlayWindow
{
    /// <summary>
    /// Alt+Tab に出さず、クリックでフォーカスも奪わないようにする。
    /// ウィンドウハンドルが生成された直後 (WPF なら SourceInitialized) に一度呼ぶ。
    /// </summary>
    public static void ApplyOverlayStyles(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        style |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
    }

    /// <summary>
    /// クリックスルーの ON / OFF。透明な余白の上ではクリックを下のウィンドウへ通し、
    /// メンダコ本体の上でだけ受け取るために動的に切り替える。
    /// </summary>
    public static void SetClickThrough(IntPtr hwnd, bool enabled)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        var updated = enabled
            ? style | NativeMethods.WS_EX_TRANSPARENT
            : style & ~(long)NativeMethods.WS_EX_TRANSPARENT;

        if (updated != style)
        {
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(updated));
        }
    }

    /// <summary>
    /// 最前面を貼り直す。タスクバー自身も TOPMOST なので、他アプリの操作で
    /// 順序が入れ替わることがある。定期的に、または WM_WINDOWPOSCHANGED で呼ぶ。
    /// </summary>
    public static void EnsureTopmost(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE
                | NativeMethods.SWP_NOSIZE
                | NativeMethods.SWP_NOACTIVATE
                | NativeMethods.SWP_NOOWNERZORDER);
    }
}

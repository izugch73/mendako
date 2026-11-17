namespace Mendako.Platform;

/// <summary>通常のウィンドウのタイトルバーを暗い配色にする。</summary>
public static class DarkTitleBar
{
    /// <summary>
    /// 失敗しても無視する。対応していない古い Windows ではタイトルバーが明るいままになるだけで、
    /// 機能には関係しない。
    /// </summary>
    public static void Apply(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var enabled = 1;
        _ = NativeMethods.DwmSetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
            ref enabled,
            sizeof(int));
    }
}

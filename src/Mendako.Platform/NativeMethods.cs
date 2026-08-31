using System.Runtime.InteropServices;

namespace Mendako.Platform;

/// <summary>
/// P/Invoke 宣言の置き場。ここだけが Win32 を直接触る。
/// 上位レイヤーはこのファイルの型を見ずに済むようにしてある。
/// </summary>
internal static class NativeMethods
{
    // --- ウィンドウスタイル ---

    internal const int GWL_EXSTYLE = -20;

    /// <summary>ピクセル単位の透過を有効にする (WPF の AllowsTransparency が内部で設定する)。</summary>
    internal const int WS_EX_LAYERED = 0x00080000;

    /// <summary>クリックを下のウィンドウへ透過させる。</summary>
    internal const int WS_EX_TRANSPARENT = 0x00000020;

    /// <summary>Alt+Tab とタスクバーに出さない。</summary>
    internal const int WS_EX_TOOLWINDOW = 0x00000080;

    /// <summary>クリックされてもフォーカスを奪わない。</summary>
    internal const int WS_EX_NOACTIVATE = 0x08000000;

    internal static readonly IntPtr HWND_TOPMOST = new(-1);

    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_NOOWNERZORDER = 0x0200;

    // --- アプリバー (タスクバー) ---

    internal const uint ABM_GETSTATE = 0x00000004;
    internal const uint ABM_GETTASKBARPOS = 0x00000005;

    internal const int ABS_AUTOHIDE = 0x0000001;

    // --- 通知状態 (全画面アプリ検出) ---

    internal const int QUNS_NOT_PRESENT = 1;
    internal const int QUNS_BUSY = 2;
    internal const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;
    internal const int QUNS_PRESENTATION_MODE = 4;
    internal const int QUNS_ACCEPTS_NOTIFICATIONS = 5;
    internal const int QUNS_QUIET_TIME = 6;
    internal const int QUNS_APP = 7;

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;

        public readonly int Height => Bottom - Top;
    }

    /// <remarks>
    /// lParam は LPARAM なので、x64 で cbSize がずれないよう必ずポインタ幅で宣言すること。
    /// int にすると SHAppBarMessage が黙って失敗する。
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [DllImport("shell32.dll", SetLastError = true)]
    internal static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    [DllImport("shell32.dll")]
    internal static extern int SHQueryUserNotificationState(out int pquns);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// <summary>32bit / 64bit の差を吸収する GetWindowLongPtr。</summary>
    internal static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : new IntPtr(GetWindowLong32(hWnd, nIndex));

    /// <summary>32bit / 64bit の差を吸収する SetWindowLongPtr。</summary>
    internal static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong) =>
        IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll", EntryPoint = "FindWindowW", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
}

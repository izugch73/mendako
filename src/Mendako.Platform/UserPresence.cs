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
    public static bool ShouldHideOverlay() => Query() switch
    {
        PresenceState.FullScreenApp => true,
        PresenceState.Presentation => true,
        PresenceState.Busy => true,
        _ => false,
    };
}

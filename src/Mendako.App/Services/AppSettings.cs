namespace Mendako.App.Services;

/// <summary>ユーザー設定。育成状態とは別ファイルに保存する。</summary>
public sealed record AppSettings
{
    /// <summary>
    /// タスクバー上の横位置を 0.0 - 1.0 の比率で保持する。
    /// 絶対座標で持つと解像度やモニタ構成が変わったときに画面外へ消える。
    /// </summary>
    public double PositionRatio { get; init; } = 0.72d;

    /// <summary>表示倍率。</summary>
    public double Scale { get; init; } = 1.0d;

    /// <summary>全画面アプリ・プレゼン中に隠すか。</summary>
    public bool HideOnFullScreen { get; init; } = true;

    /// <summary>カーソルを乗せたときにステータスを出すか。</summary>
    public bool ShowStatusOnHover { get; init; } = true;
}

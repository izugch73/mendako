namespace Mendako.Core.Behavior;

/// <summary>
/// 1 フレーム分の見た目。ドット絵なので、回転や非整数の拡縮は持たせず
/// 「どのコマか」と「何ドットずらすか」だけで表現する。
/// </summary>
public readonly record struct PetPose
{
    /// <summary>耳ビレのコマ。</summary>
    public FinPose Fin { get; init; }

    /// <summary>目のコマ。</summary>
    public EyePose Eyes { get; init; }

    /// <summary>視線。-1 で左、0 で正面、+1 で右。目が開いているときだけ意味を持つ。</summary>
    public int GazeDots { get; init; }

    /// <summary>ぺたんと平たくなっているか。</summary>
    public bool Flat { get; init; }

    /// <summary>上下の浮遊オフセット（ドット単位、負で上）。描画側で整数に丸める。</summary>
    public double BobDots { get; init; }

    /// <summary>左右のオフセット（ドット単位）。</summary>
    public double DriftDots { get; init; }

    /// <summary>
    /// このフレームでタスクバーに沿って進む距離（ドット単位、正で終端側）。
    /// <see cref="DriftDots"/> と違って元の位置には戻らない。居場所そのものが動く。
    /// </summary>
    public double TravelDots { get; init; }

    /// <summary>zZZ を出すか。</summary>
    public bool ShowSleepMark { get; init; }

    /// <summary>ハートを出すか。</summary>
    public bool ShowHeart { get; init; }

    public static PetPose Neutral => new()
    {
        Fin = FinPose.Mid,
        Eyes = EyePose.Open,
    };
}

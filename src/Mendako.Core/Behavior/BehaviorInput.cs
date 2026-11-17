namespace Mendako.Core.Behavior;

/// <summary>
/// そのフレームのまわりの状況。ウィンドウ側が測って渡す。
/// 既定値は「何も見えず、どこにも動けない」なので、渡さなければ視線も散歩も起きない。
/// </summary>
public readonly record struct BehaviorInput
{
    /// <summary>メンダコの中心から見たカーソルの位置（ドット単位、右と下が正）。分からなければ null。</summary>
    public (double X, double Y)? CursorDots { get; init; }

    /// <summary>カーソルが乗っている、またはつかまれている。逃げるような動きは控える。</summary>
    public bool Hovering { get; init; }

    /// <summary>タスクバーの始端側（左または上）に動ける余地（ドット単位）。</summary>
    public double RoomBeforeDots { get; init; }

    /// <summary>タスクバーの終端側（右または下）に動ける余地（ドット単位）。</summary>
    public double RoomAfterDots { get; init; }

    /// <summary>タスクバーが縦置きか。進行方向に目を向けるかどうかの判断に使う。</summary>
    public bool VerticalRail { get; init; }

    /// <summary>
    /// 同じ足場に置いてあるアイテム。置いていなければ null。
    /// 別のモニタの足場にあるものは含めない (泳いでいけないので)。
    /// </summary>
    public IReadOnlyList<ScatterTarget>? Scattered { get; init; }
}

/// <summary>メンダコから見た、置いてあるアイテム 1 個の位置。</summary>
/// <param name="Id">置いたアイテムの識別子 (<see cref="Model.ScatteredItem.Id"/>)。</param>
/// <param name="OffsetDots">メンダコの中心からアイテムの中心までの距離（ドット単位、終端側が正）。</param>
public readonly record struct ScatterTarget(Guid Id, double OffsetDots);

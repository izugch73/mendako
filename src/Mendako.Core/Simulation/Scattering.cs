using Mendako.Core.Model;

namespace Mendako.Core.Simulation;

/// <summary>置いたアイテムの操作。置く・片付ける・メンダコが近寄ったときの反応。</summary>
public static class Scattering
{
    /// <summary>持ち物のアイテムを 1 個、タスクバーに置く。</summary>
    /// <param name="position">足場の全長に対する中心の位置。範囲外は端に寄せる。</param>
    /// <param name="id">置いた 1 個の識別子。省略すると新しく振る (テストで固定したいときだけ渡す)。</param>
    public static ScatterOutcome Place(
        MendakoState state,
        ItemId item,
        double position,
        string? monitorId,
        SimulationConfig? config = null,
        Guid? id = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        config ??= SimulationConfig.Default;

        var index = IndexOf(state.Items, item);
        if (index < 0)
        {
            return new ScatterOutcome(state, ScatterResult.NotInInventory, null);
        }

        if (state.ScatteredItems.Count >= config.MaxScatteredItems)
        {
            return new ScatterOutcome(state, ScatterResult.TooMany, null);
        }

        var placed = new ScatteredItem
        {
            Id = id ?? Guid.NewGuid(),
            Item = item,
            Position = Math.Clamp(double.IsNaN(position) ? 0.5d : position, 0d, 1d),
            MonitorId = monitorId,
        };

        var remaining = state.Items.ToList();
        remaining.RemoveAt(index);

        var next = state with
        {
            Items = remaining,
            ScatteredItems = state.ScatteredItems.Append(placed).ToArray(),
        };

        return new ScatterOutcome(next, ScatterResult.Placed, placed);
    }

    /// <summary>置いてあるアイテムを持ち物に戻す。持ち物がいっぱいなら戻せず、置いたままにする。</summary>
    public static ScatterOutcome PickUp(MendakoState state, Guid id, SimulationConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        config ??= SimulationConfig.Default;

        var found = Find(state, id);
        if (found is null)
        {
            return new ScatterOutcome(state, ScatterResult.NotFound, null);
        }

        if (state.Items.Count >= config.MaxInventoryItems)
        {
            return new ScatterOutcome(state, ScatterResult.InventoryFull, found);
        }

        var next = state with
        {
            Items = state.Items.Append(found.Item).ToArray(),
            ScatteredItems = state.ScatteredItems.Where(s => s.Id != id).ToArray(),
        };

        return new ScatterOutcome(next, ScatterResult.PickedUp, found);
    }

    /// <summary>
    /// メンダコが置いたアイテムのそばに着いたときの反応。分類で決まる。
    /// 食べ物はごはんと同じ効果で、満腹だと食べずに残す。それ以外は残る。
    /// </summary>
    public static ScatterReactionOutcome React(
        MendakoState state,
        Guid id,
        DateTimeOffset nowUtc,
        SimulationConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        var found = Find(state, id);
        if (found is null || state.IsAsleep)
        {
            return new ScatterReactionOutcome(state, ScatterReaction.None, found);
        }

        switch (ItemCatalog.Get(found.Item).Category)
        {
            case ItemCategory.Food:
                var fed = Simulator.Feed(state, nowUtc, config);
                if (!fed.Changed)
                {
                    return new ScatterReactionOutcome(state, ScatterReaction.RefusedTooFull, found);
                }

                var eaten = fed.State with { ScatteredItems = state.ScatteredItems.Where(s => s.Id != id).ToArray() };
                return new ScatterReactionOutcome(eaten, ScatterReaction.Ate, found);

            case ItemCategory.Material:
                return new ScatterReactionOutcome(state, ScatterReaction.Delighted, found);

            case ItemCategory.Treasure:
                return new ScatterReactionOutcome(state, ScatterReaction.Admired, found);

            default:
                return new ScatterReactionOutcome(state, ScatterReaction.Poked, found);
        }
    }

    /// <summary>
    /// 置く位置を決める。メンダコから少し離れた左右どちらかで、すでに置いてあるものと重ならない場所。
    /// 単位は呼び出し側が揃えればよい (ピクセルでもドットでも)。
    /// </summary>
    /// <param name="petCentre">メンダコの中心。</param>
    /// <param name="min">置いてよい範囲の左端 (中心の位置)。</param>
    /// <param name="max">置いてよい範囲の右端 (中心の位置)。</param>
    /// <param name="occupied">すでに置いてあるアイテムの中心。</param>
    /// <param name="distance">メンダコからこれだけは離す。</param>
    /// <param name="minGap">アイテム同士の中心の間隔の下限。</param>
    public static double ChoosePosition(
        double petCentre,
        double min,
        double max,
        IReadOnlyList<double> occupied,
        double distance,
        double minGap,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(occupied);
        ArgumentNullException.ThrowIfNull(random);

        if (max < min)
        {
            return (min + max) / 2d;
        }

        bool Free(double x) => x >= min && x <= max && occupied.All(o => Math.Abs(o - x) >= minGap);

        // 左右どちらから試すかだけを乱数で決める。離れる量は決め打ちにして、置く場所が読めるようにする
        var firstSign = random.Next(2) == 0 ? -1d : 1d;
        var step = Math.Max(1d, minGap);

        for (var ring = 0; ring < 6; ring++)
        {
            foreach (var sign in new[] { firstSign, -firstSign })
            {
                var x = petCentre + (sign * (distance + (ring * step)));
                if (Free(x))
                {
                    return x;
                }
            }
        }

        // 両側が詰まっているときは、空いている範囲からメンダコに近すぎない場所を探す
        var best = double.NaN;
        var bestScore = double.NegativeInfinity;
        for (var x = min; x <= max; x += step / 4d)
        {
            if (!Free(x))
            {
                continue;
            }

            var away = Math.Abs(x - petCentre);
            var score = away >= distance ? -away : away - (distance * 100d);
            if (score > bestScore)
            {
                bestScore = score;
                best = x;
            }
        }

        return double.IsNaN(best) ? Math.Clamp(petCentre + (firstSign * distance), min, max) : best;
    }

    private static ScatteredItem? Find(MendakoState state, Guid id)
    {
        foreach (var scattered in state.ScatteredItems)
        {
            if (scattered.Id == id)
            {
                return scattered;
            }
        }

        return null;
    }

    private static int IndexOf(IReadOnlyList<ItemId> items, ItemId item)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] == item)
            {
                return i;
            }
        }

        return -1;
    }
}

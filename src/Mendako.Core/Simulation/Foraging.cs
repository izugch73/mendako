using Mendako.Core.Model;

namespace Mendako.Core.Simulation;

/// <summary>ぬしが拾うアイテムの抽選と、拾う時刻の計算。</summary>
internal static class Foraging
{
    internal sealed record Result(IReadOnlyList<ForagedItem> Foraged, int Missed, DateTimeOffset? LastForageUtc)
    {
        /// <summary>何も拾わず、拾えなかった分もない結果。</summary>
        public static Result None(DateTimeOffset? lastForageUtc) => new(Array.Empty<ForagedItem>(), 0, lastForageUtc);
    }

    /// <summary>
    /// 拾う時刻は <paramref name="anchor"/> から <see cref="SimulationConfig.ForageInterval"/> ごとに決まる。
    /// 持ち物がいっぱいで拾えなかった時刻も「過ぎたもの」として基準を進めるので、
    /// 売ったあとに溜まっていた分をまとめて拾うことはない。
    /// </summary>
    /// <param name="anchor">拾う時刻の数え始め。ぬしになった時刻、または最後に拾う機会があった時刻。</param>
    /// <param name="windowStart">
    /// これより後の時刻だけを拾う対象にする。オフライン進行の上限で切り捨てた古い時間を拾わせないための下限。
    /// </param>
    internal static Result Resolve(
        DateTimeOffset anchor,
        DateTimeOffset windowStart,
        DateTimeOffset end,
        int freeSlots,
        SimulationConfig config,
        Random random)
    {
        var interval = config.ForageInterval.Ticks;
        if (interval <= 0 || end < anchor)
        {
            // 時計の巻き戻しなどで基準が未来にあるときは、巻き戻した分の得をさせない
            return Result.None(anchor);
        }

        var lower = windowStart > anchor ? windowStart : anchor;
        var firstIndex = ((lower - anchor).Ticks / interval) + 1;
        var lastIndex = (end - anchor).Ticks / interval;
        var due = (int)Math.Max(0, lastIndex - firstIndex + 1);
        var picked = Math.Min(due, Math.Max(0, freeSlots));

        var foraged = new List<ForagedItem>(picked);
        for (var n = 0; n < picked; n++)
        {
            var at = anchor.AddTicks((firstIndex + n) * interval);
            foraged.Add(new ForagedItem(Roll(random, config), at));
        }

        return new Result(foraged, due - picked, anchor.AddTicks(lastIndex * interval));
    }

    /// <summary>重みつきの抽選。</summary>
    internal static ItemId Roll(Random random, SimulationConfig config)
    {
        var all = ItemCatalog.All;
        var total = 0d;
        foreach (var item in all)
        {
            total += config.ForageWeightOf(item.Frequency);
        }

        var roll = random.NextDouble() * total;
        foreach (var item in all)
        {
            roll -= config.ForageWeightOf(item.Frequency);
            if (roll < 0d)
            {
                return item.Id;
            }
        }

        // 浮動小数点の丸めで最後まで引き切れなかったときの保険
        return all[^1].Id;
    }
}

using Mendako.Core.Model;
using Mendako.Core.Simulation;
using Xunit;

namespace Mendako.Core.Tests;

/// <summary>アイテムを拾う仕組みのテスト。時間を進めて検証する。</summary>
public class ForagingTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly double AdultGrowth = GrowthStages.Thresholds[(int)GrowthStage.Adult];

    private static readonly double ElderGrowth = GrowthStages.Thresholds[(int)GrowthStage.Elder];

    private static readonly SimulationConfig Roomy = SimulationConfig.Default with { MaxInventoryItems = 1000 };

    private static MendakoState ElderAt(DateTimeOffset at) => MendakoState.CreateNew(at) with
    {
        Growth = ElderGrowth,
        Satiety = 100d,
        Energy = 100d,
        Affection = 100d,
    };

    private static MendakoState AdultAt(DateTimeOffset at) => ElderAt(at) with { Growth = AdultGrowth };

    private static AdvanceOutcome AdvanceBy(
        MendakoState state,
        TimeSpan span,
        SimulationConfig? config = null,
        int seed = 1) =>
        Simulator.Advance(state, state.LastTickUtc + span, TimeZoneInfo.Utc, config, new Random(seed));

    [Fact]
    public void 拾い始める段階より前は拾わない()
    {
        var state = MendakoState.CreateNew(Noon) with
        {
            Growth = GrowthStages.Thresholds[(int)GrowthStage.Juvenile],
            Satiety = 100d,
            Energy = 100d,
            Affection = 100d,
        };

        // Juvenile から 24 時間では Adult に届かない
        var outcome = AdvanceBy(state, TimeSpan.FromHours(24));

        Assert.Equal(GrowthStage.Juvenile, outcome.State.Stage);
        Assert.Empty(outcome.Foraged);
        Assert.Empty(outcome.State.Items);
        Assert.Null(outcome.State.LastForageUtc);
    }

    [Fact]
    public void Adultで拾う()
    {
        var outcome = AdvanceBy(AdultAt(Noon), TimeSpan.FromHours(3));

        Assert.Equal(GrowthStage.Adult, outcome.State.Stage);
        Assert.Single(outcome.Foraged);
        Assert.Single(outcome.State.Items);
    }

    [Fact]
    public void 拾い始める段階は設定で変えられる()
    {
        var config = SimulationConfig.Default with { ForageFromStage = GrowthStage.Elder };

        var outcome = AdvanceBy(AdultAt(Noon), TimeSpan.FromHours(3), config);

        Assert.Empty(outcome.Foraged);
    }

    [Fact]
    public void Adultになる前の時間は拾う時間に数えない()
    {
        // Adult の手前から始めて、最初のティックの終わり (12:01) に Adult になる
        var state = AdultAt(Noon) with { Growth = AdultGrowth - 0.5d };

        // 12:00 から数えていれば 15:00 に拾うが、数え始めは 12:01 なので 15:01 まで拾わない
        var early = AdvanceBy(state, TimeSpan.FromHours(3));
        Assert.Equal(GrowthStage.Adult, early.State.Stage);
        Assert.Empty(early.Foraged);

        var later = AdvanceBy(state, TimeSpan.FromHours(3) + TimeSpan.FromMinutes(30));
        var only = Assert.Single(later.Foraged);
        Assert.Equal(Noon.AddMinutes(1 + 180), only.AtUtc);
    }

    [Fact]
    public void 拾い始めても3時間たつまでは拾わない()
    {
        var outcome = AdvanceBy(ElderAt(Noon), TimeSpan.FromMinutes(179));

        Assert.Empty(outcome.Foraged);
        Assert.Empty(outcome.State.Items);
    }

    [Fact]
    public void 三時間ごとに1個ずつ拾う()
    {
        var outcome = AdvanceBy(ElderAt(Noon), TimeSpan.FromHours(9));

        Assert.Equal(3, outcome.Foraged.Count);
        Assert.Equal(
            new[] { Noon.AddHours(3), Noon.AddHours(6), Noon.AddHours(9) },
            outcome.Foraged.Select(f => f.AtUtc));
        Assert.Equal(outcome.Foraged.Select(f => f.Item), outcome.State.Items);
        Assert.Equal(Noon.AddHours(9), outcome.State.LastForageUtc);
        Assert.Equal(0, outcome.ForageMissed);
    }

    [Fact]
    public void 三時間に満たない端数は次回へ持ち越される()
    {
        var first = AdvanceBy(ElderAt(Noon), TimeSpan.FromHours(4));
        Assert.Single(first.Foraged);

        // 3 時間目に 1 個拾ったので、次は 6 時間目。あと 1 時間では拾わない
        var second = AdvanceBy(first.State, TimeSpan.FromHours(1));
        Assert.Empty(second.Foraged);

        var third = AdvanceBy(second.State, TimeSpan.FromHours(1));
        var only = Assert.Single(third.Foraged);
        Assert.Equal(Noon.AddHours(6), only.AtUtc);
        Assert.Equal(2, third.State.Items.Count);
    }

    [Fact]
    public void 閉じていた間の分もまとめて拾う()
    {
        var outcome = AdvanceBy(ElderAt(Noon), TimeSpan.FromDays(2), Roomy);

        Assert.Equal(16, outcome.Foraged.Count);
        Assert.Equal(0, outcome.TicksSkipped);
    }

    [Fact]
    public void 閉じていた間の分は三日で打ち切られる()
    {
        var outcome = AdvanceBy(ElderAt(Noon), TimeSpan.FromDays(10), Roomy);

        Assert.True(outcome.TicksSkipped > 0);
        Assert.Equal(24, outcome.Foraged.Count);
        Assert.Equal(0, outcome.ForageMissed);
        Assert.Equal(Noon.AddDays(10), outcome.State.LastForageUtc);
    }

    [Fact]
    public void 長く閉じても拾う時刻の周期はずれない()
    {
        var state = ElderAt(Noon) with { LastForageUtc = Noon };

        // 対象は最後の 3 日分だけで、その最初は 7 日後の 15:00
        var outcome = AdvanceBy(state, TimeSpan.FromDays(10), Roomy);

        Assert.Equal(Noon.AddDays(7).AddHours(3), outcome.Foraged[0].AtUtc);
        Assert.Equal(Noon.AddDays(10), outcome.Foraged[^1].AtUtc);
    }

    [Fact]
    public void 持ち物がいっぱいなら拾わない()
    {
        var full = ElderAt(Noon) with { Items = Enumerable.Repeat(ItemId.Apple, 20).ToArray() };

        var outcome = AdvanceBy(full, TimeSpan.FromHours(6));

        Assert.Empty(outcome.Foraged);
        Assert.Equal(20, outcome.State.Items.Count);
        Assert.Equal(2, outcome.ForageMissed);
    }

    [Fact]
    public void 持ち物は上限までしか増えない()
    {
        var outcome = AdvanceBy(ElderAt(Noon), TimeSpan.FromDays(10));

        Assert.Equal(20, outcome.State.Items.Count);
        Assert.Equal(20, outcome.Foraged.Count);
        Assert.Equal(4, outcome.ForageMissed);
    }

    [Fact]
    public void 上限に余りがあるぶんだけ拾い_古い時刻から埋まる()
    {
        var state = ElderAt(Noon) with { Items = Enumerable.Repeat(ItemId.Apple, 18).ToArray() };

        var outcome = AdvanceBy(state, TimeSpan.FromHours(9));

        Assert.Equal(new[] { Noon.AddHours(3), Noon.AddHours(6) }, outcome.Foraged.Select(f => f.AtUtc));
        Assert.Equal(1, outcome.ForageMissed);
    }

    [Fact]
    public void 拾えなかった分は売って空きができても取り戻さない()
    {
        var full = ElderAt(Noon) with { Items = Enumerable.Repeat(ItemId.Apple, 20).ToArray() };
        var missed = AdvanceBy(full, TimeSpan.FromHours(6));

        var emptied = missed.State with { Items = Array.Empty<ItemId>() };

        // 12:00-18:00 の 2 回ぶんは失われている。次に拾うのは 21:00
        var soon = AdvanceBy(emptied, TimeSpan.FromHours(2));
        Assert.Empty(soon.Foraged);

        var next = AdvanceBy(soon.State, TimeSpan.FromHours(1));
        var only = Assert.Single(next.Foraged);
        Assert.Equal(Noon.AddHours(9), only.AtUtc);
    }

    [Fact]
    public void 基準時刻の無いAdult以上の保存データは進行の起点から数え始める()
    {
        var state = ElderAt(Noon);
        Assert.Null(state.LastForageUtc);

        var outcome = AdvanceBy(state, TimeSpan.FromHours(3));

        Assert.Single(outcome.Foraged);
    }

    [Fact]
    public void シードが同じなら同じアイテムを拾う()
    {
        var a = AdvanceBy(ElderAt(Noon), TimeSpan.FromDays(3), seed: 42);
        var b = AdvanceBy(ElderAt(Noon), TimeSpan.FromDays(3), seed: 42);
        var c = AdvanceBy(ElderAt(Noon), TimeSpan.FromDays(3), seed: 43);

        Assert.Equal(a.State.Items, b.State.Items);
        Assert.NotEqual(a.State.Items, c.State.Items);
    }

    [Fact]
    public void 抽選は頻度の重みに従う()
    {
        var random = new Random(7);
        var config = SimulationConfig.Default;
        var draws = Enumerable.Range(0, 20_000)
            .Select(_ => ItemCatalog.Get(Foraging.Roll(random, config)).Category)
            .ToList();

        var common = draws.Count(c => c is ItemCategory.Food or ItemCategory.Material) / (double)draws.Count;
        var treasure = draws.Count(c => c == ItemCategory.Treasure) / (double)draws.Count;

        Assert.InRange(common, 0.70, 0.79);
        Assert.InRange(treasure, 0.01, 0.045);
    }

    [Fact]
    public void 重みが0の頻度のアイテムは拾わない()
    {
        var config = SimulationConfig.Default with { CommonForageWeight = 0d, RareForageWeight = 0d };
        var random = new Random(3);

        var categories = Enumerable.Range(0, 500)
            .Select(_ => ItemCatalog.Get(Foraging.Roll(random, config)).Category)
            .Distinct();

        Assert.Equal(new[] { ItemCategory.Gear }, categories);
    }

    [Fact]
    public void 時計を巻き戻しても拾う基準が未来に残らない()
    {
        var state = ElderAt(Noon);

        // 6 時間進めて拾う。基準 (LastForageUtc) は 18:00 になる
        var first = AdvanceBy(state, TimeSpan.FromHours(6), Roomy);
        Assert.Equal(2, first.Foraged.Count);
        Assert.Equal(Noon.AddHours(6), first.State.LastForageUtc);

        // 12:00 まで巻き戻す。基準だけが未来に残ってはいけない
        var rewound = Simulator.Advance(first.State, Noon, TimeZoneInfo.Utc, Roomy, new Random(1));
        Assert.Equal(Noon, rewound.State.LastTickUtc);
        Assert.Equal(Noon, rewound.State.LastForageUtc);
        Assert.Equal(first.State.Items, rewound.State.Items);

        // そこから 6 時間進めると、また拾う
        var again = AdvanceBy(rewound.State, TimeSpan.FromHours(6), Roomy);
        Assert.Equal(2, again.Foraged.Count);
    }
}

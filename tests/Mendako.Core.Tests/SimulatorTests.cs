using Mendako.Core.Model;
using Mendako.Core.Simulation;
using Xunit;

namespace Mendako.Core.Tests;

/// <summary>
/// 育成ロジックのテスト。UI も時計も要らないので、
/// 「3 日放置したらどうなるか」を実時間を待たずに検証できる。
/// </summary>
public class SimulatorTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static MendakoState NewStateAt(DateTimeOffset at) => MendakoState.CreateNew(at);

    private static AdvanceOutcome AdvanceBy(
        MendakoState state,
        TimeSpan span,
        SimulationConfig? config = null) =>
        Simulator.Advance(state, state.LastTickUtc + span, TimeZoneInfo.Utc, config);

    // --- 時間経過 ---

    [Fact]
    public void 経過時間がティック未満なら状態は変わらない()
    {
        var state = NewStateAt(Noon);

        var outcome = AdvanceBy(state, TimeSpan.FromSeconds(30));

        Assert.Equal(0, outcome.TicksApplied);
        Assert.Same(state, outcome.State);
    }

    [Fact]
    public void ティック未満の端数は切り捨てずに次回へ持ち越される()
    {
        var state = NewStateAt(Noon);

        // 90 秒 = 1 ティック + 30 秒。基準時刻は 1 分だけ進むべき
        var outcome = AdvanceBy(state, TimeSpan.FromSeconds(90));

        Assert.Equal(1, outcome.TicksApplied);
        Assert.Equal(Noon.AddMinutes(1), outcome.State.LastTickUtc);

        // 残りの 30 秒はまだ消化されていないので、さらに 30 秒でもう 1 ティック進む
        var next = Simulator.Advance(
            outcome.State,
            Noon.AddSeconds(120),
            TimeZoneInfo.Utc);

        Assert.Equal(1, next.TicksApplied);
    }

    [Fact]
    public void 満腹度はおよそ24時間で切れる()
    {
        var state = NewStateAt(Noon) with { Satiety = 100d, Energy = 100d };

        var outcome = AdvanceBy(state, TimeSpan.FromHours(24));

        // 夜間は睡眠で消費が半分になるので、ちょうど 0 にはならない
        Assert.InRange(outcome.State.Satiety, 0d, 30d);
    }

    [Fact]
    public void 満腹度は0を下回らない()
    {
        var state = NewStateAt(Noon) with { Satiety = 5d };

        var outcome = AdvanceBy(state, TimeSpan.FromHours(20));

        Assert.Equal(0d, outcome.State.Satiety);
    }

    // --- オフライン進行の上限 ---

    [Fact]
    public void 長期不在でもキャッチアップは上限で打ち切られる()
    {
        var config = SimulationConfig.Default with { MaxCatchUpTicks = 60 };
        var state = NewStateAt(Noon);

        var outcome = AdvanceBy(state, TimeSpan.FromDays(30), config);

        Assert.Equal(60, outcome.TicksApplied);
        Assert.Equal((30 * 24 * 60) - 60, outcome.TicksSkipped);
    }

    [Fact]
    public void 打ち切っても基準時刻は現在まで進む()
    {
        var config = SimulationConfig.Default with { MaxCatchUpTicks = 60 };
        var state = NewStateAt(Noon);

        var outcome = AdvanceBy(state, TimeSpan.FromDays(30), config);

        // ここを applied 分しか進めないと、次回起動で同じ時間をもう一度消化してしまう
        Assert.Equal(Noon.AddDays(30), outcome.State.LastTickUtc);
    }

    [Fact]
    public void 二週間放置しても手遅れにはならない()
    {
        var state = NewStateAt(Noon) with { Satiety = 100d, Affection = 80d };

        var outcome = AdvanceBy(state, TimeSpan.FromDays(14));

        // パラメータは下がりきるが、状態としては生きていて回復可能
        Assert.Equal(0d, outcome.State.Satiety);
        Assert.True(outcome.State.Affection > 0d);

        var fed = Simulator.Feed(
            Simulator.SetAsleep(outcome.State, asleep: false),
            outcome.State.LastTickUtc);

        Assert.Equal(CareResult.Accepted, fed.Result);
        Assert.True(fed.State.Satiety > 0d);
    }

    // --- 時計いじり ---

    [Fact]
    public void 時計が巻き戻されても状態は進まない()
    {
        var state = NewStateAt(Noon) with { Satiety = 50d };

        var outcome = Simulator.Advance(state, Noon.AddHours(-5), TimeZoneInfo.Utc);

        Assert.Equal(0, outcome.TicksApplied);
        Assert.Equal(50d, outcome.State.Satiety);

        // 基準時刻だけは現在に合わせ直し、巻き戻し分を後で二重に消化しないようにする
        Assert.Equal(Noon.AddHours(-5), outcome.State.LastTickUtc);
    }

    [Fact]
    public void 時計を進めても上限を超えて成長させられない()
    {
        var config = SimulationConfig.Default with { MaxCatchUpTicks = 100 };
        var state = NewStateAt(Noon) with { Satiety = 100d, Energy = 100d, Affection = 100d };

        var cheated = AdvanceBy(state, TimeSpan.FromDays(365), config);

        Assert.True(cheated.State.Growth <= 100d * config.MaxGrowthPerTick);
    }

    // --- 成長 ---

    [Fact]
    public void 世話が行き届いていれば成長する()
    {
        var state = NewStateAt(Noon) with { Satiety = 100d, Energy = 100d, Affection = 100d };

        var outcome = AdvanceBy(state, TimeSpan.FromHours(2));

        Assert.True(outcome.State.Growth > 0d);
    }

    [Fact]
    public void 空腹のあいだは成長しない()
    {
        var config = SimulationConfig.Default;
        var state = NewStateAt(Noon) with
        {
            Satiety = config.GrowthSatietyFloor,
            Energy = 100d,
            Affection = 100d,
        };

        var outcome = AdvanceBy(state, TimeSpan.FromHours(3), config);

        Assert.Equal(0d, outcome.State.Growth);
    }

    [Fact]
    public void 成長ポイントは減らない()
    {
        var state = NewStateAt(Noon) with { Growth = 5_000d, Satiety = 0d, Affection = 0d };

        var outcome = AdvanceBy(state, TimeSpan.FromDays(2));

        Assert.True(outcome.State.Growth >= 5_000d);
    }

    [Fact]
    public void 段階が上がったらそれが報告される()
    {
        var justBelow = GrowthStages.Thresholds[(int)GrowthStage.Hatchling] - 5d;
        var state = NewStateAt(Noon) with
        {
            Growth = justBelow,
            Satiety = 100d,
            Energy = 100d,
            Affection = 100d,
        };

        var outcome = AdvanceBy(state, TimeSpan.FromMinutes(30));

        Assert.Equal(GrowthStage.Hatchling, outcome.StageAdvancedTo);
        Assert.Equal(GrowthStage.Hatchling, outcome.State.Stage);
    }

    [Fact]
    public void 段階が変わらなければ報告されない()
    {
        var state = NewStateAt(Noon) with { Satiety = 100d, Energy = 100d };

        var outcome = AdvanceBy(state, TimeSpan.FromMinutes(10));

        Assert.Null(outcome.StageAdvancedTo);
    }

    // --- 睡眠 ---

    [Fact]
    public void 夜になると眠る()
    {
        var midnight = new DateTimeOffset(2026, 6, 1, 23, 30, 0, TimeSpan.Zero);
        var state = MendakoState.CreateNew(midnight) with { Energy = 50d };

        var outcome = AdvanceBy(state, TimeSpan.FromMinutes(5));

        Assert.True(outcome.State.IsAsleep);
        Assert.True(outcome.FellAsleep);
    }

    [Fact]
    public void 昼間に元気なら眠らない()
    {
        var state = NewStateAt(Noon) with { Energy = 90d };

        var outcome = AdvanceBy(state, TimeSpan.FromMinutes(5));

        Assert.False(outcome.State.IsAsleep);
    }

    [Fact]
    public void 元気が尽きれば昼でも眠る()
    {
        var state = NewStateAt(Noon) with { Energy = 10d };

        var outcome = AdvanceBy(state, TimeSpan.FromMinutes(2));

        Assert.True(outcome.State.IsAsleep);
    }

    [Fact]
    public void 睡眠中は元気が回復する()
    {
        var state = NewStateAt(Noon) with { Energy = 10d, Satiety = 100d };

        var outcome = AdvanceBy(state, TimeSpan.FromHours(4));

        Assert.True(outcome.State.Energy > 10d);
    }

    [Fact]
    public void 朝になって回復していれば目を覚ます()
    {
        var beforeDawn = new DateTimeOffset(2026, 6, 1, 6, 30, 0, TimeSpan.Zero);
        var state = MendakoState.CreateNew(beforeDawn) with
        {
            Energy = 95d,
            Satiety = 100d,
            IsAsleep = true,
        };

        var outcome = AdvanceBy(state, TimeSpan.FromHours(1));

        Assert.False(outcome.State.IsAsleep);
        Assert.True(outcome.WokeUp);
    }

    [Theory]
    [InlineData(23, true)]
    [InlineData(2, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    [InlineData(12, false)]
    [InlineData(22, false)]
    public void 日をまたぐ夜の時間帯を正しく判定する(int hour, bool expected)
    {
        Assert.Equal(expected, Simulator.IsNight(hour, SimulationConfig.Default));
    }

    // --- お世話 ---

    [Fact]
    public void エサをあげると満腹度となつき度が上がる()
    {
        var state = NewStateAt(Noon) with { Satiety = 40d, Affection = 10d };

        var outcome = Simulator.Feed(state, Noon);

        Assert.Equal(CareResult.Accepted, outcome.Result);
        Assert.True(outcome.State.Satiety > 40d);
        Assert.True(outcome.State.Affection > 10d);
        Assert.Equal(1, outcome.State.FeedCount);
    }

    [Fact]
    public void 満腹ならエサを断る()
    {
        var state = NewStateAt(Noon) with { Satiety = 95d };

        var outcome = Simulator.Feed(state, Noon);

        Assert.Equal(CareResult.RefusedTooFull, outcome.Result);
        Assert.Same(state, outcome.State);
    }

    [Fact]
    public void 就寝中はエサを断る()
    {
        var state = NewStateAt(Noon) with { Satiety = 10d, IsAsleep = true };

        var outcome = Simulator.Feed(state, Noon);

        Assert.Equal(CareResult.RefusedAsleep, outcome.Result);
    }

    [Fact]
    public void 満腹度は100を超えない()
    {
        var state = NewStateAt(Noon) with { Satiety = 89d };

        var outcome = Simulator.Feed(state, Noon);

        Assert.Equal(CareResult.Accepted, outcome.Result);
        Assert.Equal(100d, outcome.State.Satiety);
    }

    [Fact]
    public void なでるとなつき度が上がる()
    {
        var state = NewStateAt(Noon) with { Affection = 10d };

        var outcome = Simulator.Pet(state, Noon);

        Assert.Equal(CareResult.Accepted, outcome.Result);
        Assert.True(outcome.State.Affection > 10d);
    }

    [Fact]
    public void クールダウン中になでても反応はするがなつき度は上がらない()
    {
        var state = NewStateAt(Noon) with { Affection = 10d };

        var first = Simulator.Pet(state, Noon);
        var second = Simulator.Pet(first.State, Noon.AddMinutes(1));

        Assert.Equal(CareResult.AcceptedWithoutGain, second.Result);
        Assert.Equal(first.State.Affection, second.State.Affection);
        Assert.Equal(2, second.State.PetCount);
    }

    [Fact]
    public void クールダウンが明ければまたなつき度が上がる()
    {
        var config = SimulationConfig.Default;
        var state = NewStateAt(Noon) with { Affection = 10d };

        var first = Simulator.Pet(state, Noon, config);
        var second = Simulator.Pet(first.State, Noon + config.PetCooldown, config);

        Assert.Equal(CareResult.Accepted, second.Result);
        Assert.True(second.State.Affection > first.State.Affection);
    }

    [Fact]
    public void 名前は前後の空白を落として設定される()
    {
        var state = NewStateAt(Noon);

        var renamed = Simulator.Rename(state, "  たこすけ  ");

        Assert.Equal("たこすけ", renamed.Name);
    }

    [Fact]
    public void 空の名前では改名しない()
    {
        var state = NewStateAt(Noon) with { Name = "めんちゃん" };

        var renamed = Simulator.Rename(state, "   ");

        Assert.Equal("めんちゃん", renamed.Name);
    }
}

using System.Text.Json;
using Mendako.Core.Model;
using Mendako.Core.Simulation;
using Xunit;

namespace Mendako.Core.Tests;

/// <summary>タスクバーに置くアイテムのテスト。</summary>
public class ScatteringTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static MendakoState Elder(params ItemId[] items) => MendakoState.CreateNew(Noon) with
    {
        Growth = GrowthStages.Thresholds[(int)GrowthStage.Elder],
        Satiety = 50d,
        Items = items,
    };

    private static MendakoState WithScattered(MendakoState state, ItemId item, out Guid id)
    {
        var outcome = Scattering.Place(state, item, 0.4d, null);
        Assert.Equal(ScatterResult.Placed, outcome.Result);
        id = outcome.Item!.Id;
        return outcome.State;
    }

    // --- 置く ---

    [Fact]
    public void 置くと持ち物から1個減って置いたアイテムに加わる()
    {
        var state = Elder(ItemId.Apple, ItemId.Apple, ItemId.Sword);

        var outcome = Scattering.Place(state, ItemId.Apple, 0.25d, @"\\.\DISPLAY2");

        Assert.Equal(ScatterResult.Placed, outcome.Result);
        Assert.Equal(new[] { ItemId.Apple, ItemId.Sword }, outcome.State.Items);
        var placed = Assert.Single(outcome.State.ScatteredItems);
        Assert.Equal(ItemId.Apple, placed.Item);
        Assert.Equal(0.25d, placed.Position);
        Assert.Equal(@"\\.\DISPLAY2", placed.MonitorId);
        Assert.NotEqual(Guid.Empty, placed.Id);
    }

    [Fact]
    public void 持っていないアイテムは置けない()
    {
        var state = Elder(ItemId.Apple);

        var outcome = Scattering.Place(state, ItemId.Sword, 0.5d, null);

        Assert.Equal(ScatterResult.NotInInventory, outcome.Result);
        Assert.Same(state, outcome.State);
    }

    [Fact]
    public void 同時に置けるのは設定の個数まで()
    {
        var state = Elder(Enumerable.Repeat(ItemId.Apple, 6).ToArray());

        for (var i = 0; i < SimulationConfig.Default.MaxScatteredItems; i++)
        {
            var placed = Scattering.Place(state, ItemId.Apple, 0.1d * (i + 1), null);
            Assert.Equal(ScatterResult.Placed, placed.Result);
            state = placed.State;
        }

        var over = Scattering.Place(state, ItemId.Apple, 0.9d, null);

        Assert.Equal(3, SimulationConfig.Default.MaxScatteredItems);
        Assert.Equal(ScatterResult.TooMany, over.Result);
        Assert.Same(state, over.State);
        Assert.Equal(3, state.Items.Count);
    }

    [Fact]
    public void 置ける数は設定で変えられる()
    {
        var config = SimulationConfig.Default with { MaxScatteredItems = 1 };
        var state = Elder(ItemId.Apple, ItemId.Apple);

        var first = Scattering.Place(state, ItemId.Apple, 0.2d, null, config);
        var second = Scattering.Place(first.State, ItemId.Apple, 0.8d, null, config);

        Assert.Equal(ScatterResult.Placed, first.Result);
        Assert.Equal(ScatterResult.TooMany, second.Result);
    }

    [Fact]
    public void 位置は足場の範囲に収める()
    {
        var state = Elder(ItemId.Apple, ItemId.Apple);

        var low = Scattering.Place(state, ItemId.Apple, -3d, null);
        var high = Scattering.Place(low.State, ItemId.Apple, 7d, null);

        Assert.Equal(0d, low.Item!.Position);
        Assert.Equal(1d, high.Item!.Position);
    }

    // --- 片付ける ---

    [Fact]
    public void 片付けると持ち物に戻る()
    {
        var state = WithScattered(Elder(ItemId.Apple), ItemId.Apple, out var id);
        Assert.Empty(state.Items);

        var outcome = Scattering.PickUp(state, id);

        Assert.Equal(ScatterResult.PickedUp, outcome.Result);
        Assert.Equal(new[] { ItemId.Apple }, outcome.State.Items);
        Assert.Empty(outcome.State.ScatteredItems);
    }

    [Fact]
    public void 持ち物がいっぱいだと片付けられず置いたままになる()
    {
        var config = SimulationConfig.Default with { MaxInventoryItems = 1 };
        var state = WithScattered(Elder(ItemId.Apple), ItemId.Apple, out var id);
        state = state with { Items = new[] { ItemId.Sword } };

        var outcome = Scattering.PickUp(state, id, config);

        Assert.Equal(ScatterResult.InventoryFull, outcome.Result);
        Assert.Same(state, outcome.State);
        Assert.Single(outcome.State.ScatteredItems);
    }

    [Fact]
    public void 無いアイテムは片付けられない()
    {
        var state = Elder(ItemId.Apple);

        Assert.Equal(ScatterResult.NotFound, Scattering.PickUp(state, Guid.NewGuid()).Result);
    }

    // --- 反応 ---

    [Fact]
    public void 食べ物は食べて満腹度が上がり消える()
    {
        var state = WithScattered(Elder(ItemId.Cake), ItemId.Cake, out var id);

        var outcome = Scattering.React(state, id, Noon);

        Assert.Equal(ScatterReaction.Ate, outcome.Reaction);
        Assert.Empty(outcome.State.ScatteredItems);

        // ごはんと同じ効果
        var fed = Simulator.Feed(state, Noon);
        Assert.Equal(fed.State.Satiety, outcome.State.Satiety);
        Assert.Equal(fed.State.Affection, outcome.State.Affection);
        Assert.Equal(state.FeedCount + 1, outcome.State.FeedCount);
    }

    [Fact]
    public void 満腹の食べ物は食べずに残る()
    {
        var state = WithScattered(Elder(ItemId.Cake), ItemId.Cake, out var id) with { Satiety = 95d };

        var outcome = Scattering.React(state, id, Noon);

        Assert.Equal(ScatterReaction.RefusedTooFull, outcome.Reaction);
        Assert.Same(state, outcome.State);
        Assert.Single(outcome.State.ScatteredItems);
    }

    [Theory]
    [InlineData(ItemId.RedFlower, ScatterReaction.Delighted)]
    [InlineData(ItemId.Stone, ScatterReaction.Delighted)]
    [InlineData(ItemId.Diamond, ScatterReaction.Admired)]
    [InlineData(ItemId.WoodenChest, ScatterReaction.Admired)]
    [InlineData(ItemId.Bomb, ScatterReaction.Poked)]
    [InlineData(ItemId.Sword, ScatterReaction.Poked)]
    public void 食べ物以外は分類ごとに反応して残る(ItemId item, ScatterReaction expected)
    {
        var state = WithScattered(Elder(item), item, out var id);

        var outcome = Scattering.React(state, id, Noon);

        Assert.Equal(expected, outcome.Reaction);
        Assert.Same(state, outcome.State);
        Assert.Single(outcome.State.ScatteredItems);
    }

    [Fact]
    public void 寝ているあいだは反応しない()
    {
        var state = WithScattered(Elder(ItemId.Cake), ItemId.Cake, out var id) with { IsAsleep = true };

        var outcome = Scattering.React(state, id, Noon);

        Assert.Equal(ScatterReaction.None, outcome.Reaction);
        Assert.Single(outcome.State.ScatteredItems);
    }

    [Fact]
    public void すでに無いアイテムには反応しない()
    {
        var state = Elder();

        Assert.Equal(ScatterReaction.None, Scattering.React(state, Guid.NewGuid(), Noon).Reaction);
    }

    // --- 置く位置 ---

    [Fact]
    public void 置く位置はメンダコから離れた範囲の中になる()
    {
        for (var seed = 0; seed < 30; seed++)
        {
            var x = Scattering.ChoosePosition(500d, 100d, 900d, Array.Empty<double>(), 200d, 60d, new Random(seed));

            Assert.InRange(x, 100d, 900d);
            Assert.True(Math.Abs(x - 500d) >= 200d, $"seed {seed}: {x}");
        }
    }

    [Fact]
    public void 置く位置は置いてあるものと重ならない()
    {
        for (var seed = 0; seed < 30; seed++)
        {
            var x = Scattering.ChoosePosition(500d, 100d, 900d, new[] { 300d, 700d }, 200d, 60d, new Random(seed));

            Assert.InRange(x, 100d, 900d);
            Assert.True(Math.Abs(x - 300d) >= 60d && Math.Abs(x - 700d) >= 60d, $"seed {seed}: {x}");
        }
    }

    [Fact]
    public void 片側が壁ならもう片側に置く()
    {
        var x = Scattering.ChoosePosition(150d, 100d, 900d, Array.Empty<double>(), 200d, 60d, new Random(1));

        Assert.True(x >= 350d);
    }

    [Fact]
    public void 範囲が狭くて離れられなくても範囲の中に置く()
    {
        var x = Scattering.ChoosePosition(500d, 450d, 550d, Array.Empty<double>(), 200d, 60d, new Random(1));

        Assert.InRange(x, 450d, 550d);
    }

    // --- 保存 ---

    [Fact]
    public void 置いたアイテムの項目が無い保存データは空として読める()
    {
        const string old = """
            {
              "SchemaVersion": 1,
              "Name": "めんちゃん",
              "BornAtUtc": "2026-05-01T12:00:00+00:00",
              "LastTickUtc": "2026-06-01T12:00:00+00:00",
              "Growth": 46000,
              "Shells": 12,
              "Items": ["Apple", "Sword"]
            }
            """;

        var state = JsonSerializer.Deserialize<MendakoState>(old)!;

        Assert.Empty(state.ScatteredItems);
        Assert.Equal(new[] { ItemId.Apple, ItemId.Sword }, state.Items);
        Assert.Equal(12, state.Shells);
    }

    [Fact]
    public void 置いたアイテムは位置とモニタごと保存して読み戻せる()
    {
        var placedOutcome = Scattering.Place(Elder(ItemId.RedFlower), ItemId.RedFlower, 0.625d, @"\\.\DISPLAY2");
        var id = placedOutcome.Item!.Id;
        var state = placedOutcome.State;

        var json = JsonSerializer.Serialize(state);
        var back = JsonSerializer.Deserialize<MendakoState>(json)!;

        // 名前で保存する (並べ替えで中身が入れ替わらない)
        Assert.Contains("\"RedFlower\"", json);
        var scattered = Assert.Single(back.ScatteredItems);
        Assert.Equal(id, scattered.Id);
        Assert.Equal(ItemId.RedFlower, scattered.Item);
        Assert.Equal(0.625d, scattered.Position);
        Assert.Equal(@"\\.\DISPLAY2", scattered.MonitorId);
        Assert.Empty(back.Items);
    }

    [Fact]
    public void モニタを持たない置き方はプライマリとして保存される()
    {
        var state = Scattering.Place(Elder(ItemId.Apple), ItemId.Apple, 0.5d, null).State;

        var back = JsonSerializer.Deserialize<MendakoState>(JsonSerializer.Serialize(state))!;

        Assert.Null(Assert.Single(back.ScatteredItems).MonitorId);
    }

    [Fact]
    public void 置いたアイテムがあっても時間は進められる()
    {
        var state = WithScattered(Elder(ItemId.Apple), ItemId.Apple, out _);

        var outcome = Simulator.Advance(state, state.LastTickUtc.AddHours(1), TimeZoneInfo.Utc, random: new Random(1));

        Assert.Single(outcome.State.ScatteredItems);
    }

    // --- 読み込み後の整え ---

    private static ScatteredItem Scat(Guid id, double position, ItemId item = ItemId.Apple) =>
        new() { Id = id, Item = item, Position = position };

    [Fact]
    public void 読み込み後の整えで識別子が空や重複のものを捨てる()
    {
        var dup = Guid.NewGuid();
        var other = Guid.NewGuid();
        var state = Elder() with
        {
            ScatteredItems = new[] { Scat(Guid.Empty, 0.1d), Scat(dup, 0.2d), Scat(dup, 0.3d, ItemId.Sword), Scat(other, 0.4d) },
        };

        var normalized = Scattering.Normalize(state, new SimulationConfig { MaxScatteredItems = 10 });

        Assert.Equal(new[] { dup, other }, normalized.ScatteredItems.Select(s => s.Id));
        Assert.Equal(ItemId.Apple, normalized.ScatteredItems[0].Item);
    }

    [Fact]
    public void 読み込み後の整えで位置を0から1に丸める()
    {
        var state = Elder() with
        {
            ScatteredItems = new[]
            {
                Scat(Guid.NewGuid(), double.NaN),
                Scat(Guid.NewGuid(), -2d),
                Scat(Guid.NewGuid(), 9d),
            },
        };

        var normalized = Scattering.Normalize(state, new SimulationConfig { MaxScatteredItems = 10 });

        Assert.Equal(new[] { 0.5d, 0d, 1d }, normalized.ScatteredItems.Select(s => s.Position));
    }

    [Fact]
    public void 読み込み後の整えで上限を超えた分は持ち物に戻し持ち物がいっぱいなら捨てる()
    {
        var config = new SimulationConfig { MaxScatteredItems = 1, MaxInventoryItems = 2 };
        var state = Elder(ItemId.Sword) with
        {
            ScatteredItems = new[]
            {
                Scat(Guid.NewGuid(), 0.1d),
                Scat(Guid.NewGuid(), 0.2d, ItemId.RedFlower),
                Scat(Guid.NewGuid(), 0.3d, ItemId.Apple),
            },
        };

        var normalized = Scattering.Normalize(state, config);

        Assert.Single(normalized.ScatteredItems);
        Assert.Equal(new[] { ItemId.Sword, ItemId.RedFlower }, normalized.Items);
    }

    [Fact]
    public void 問題のない保存データは整えても同じインスタンスのまま()
    {
        var state = Elder() with { ScatteredItems = new[] { Scat(Guid.NewGuid(), 0.5d) } };

        Assert.Same(state, Scattering.Normalize(state));
    }
}

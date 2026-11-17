using System.Text.Json;
using Mendako.Core.Model;
using Xunit;

namespace Mendako.Core.Tests;

/// <summary>保存データの読み込み。読めない要素があっても、進行ごと失わない。</summary>
public class StateJsonTests
{
    private static readonly DateTimeOffset Noon = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static MendakoState Load(string json) => JsonSerializer.Deserialize<MendakoState>(json)!;

    [Fact]
    public void 書き出した状態はそのまま読み戻せる()
    {
        var state = MendakoState.CreateNew(Noon) with
        {
            Shells = 12,
            Items = new[] { ItemId.Apple, ItemId.Apple, ItemId.Diamond },
            OwnedAccessories = new[] { Accessory.Flower },
            ScatteredItems = new[] { new ScatteredItem { Id = Guid.NewGuid(), Item = ItemId.Bread, Position = 0.25 } },
        };

        var back = Load(JsonSerializer.Serialize(state));

        Assert.Equal(state.Items, back.Items);
        Assert.Equal(state.OwnedAccessories, back.OwnedAccessories);
        Assert.Equal(state.ScatteredItems, back.ScatteredItems);
        Assert.Equal(12, back.Shells);
    }

    [Fact]
    public void 未知のアイテム名と範囲外の数値は読み飛ばし_ほかは残す()
    {
        var state = Load("""{"Shells":7,"Items":["Apple","NoSuchItem",9999,-1,"Bread"]}""");

        Assert.Equal(new[] { ItemId.Apple, ItemId.Bread }, state.Items);
        Assert.Equal(7, state.Shells);
    }

    [Fact]
    public void 置いたアイテムも未知のものだけ読み飛ばす()
    {
        var keep = Guid.NewGuid();
        var json = $$"""
        {"ScatteredItems":[
          {"Id":"{{Guid.NewGuid()}}","Item":"NoSuchItem","Position":0.5},
          {"Id":"{{Guid.NewGuid()}}","Item":9999,"Position":0.5},
          null,
          {"Id":"{{keep}}","Item":"Apple","Position":0.5}
        ]}
        """;

        var placed = Assert.Single(Load(json).ScatteredItems);
        Assert.Equal(keep, placed.Id);
        Assert.Equal(ItemId.Apple, placed.Item);
    }

    [Fact]
    public void 飾りの未知の値も読み飛ばす()
    {
        var state = Load("""{"OwnedAccessories":[1,99,0,3]}""");

        Assert.Equal(new[] { Accessory.Flower, Accessory.StrawHat }, state.OwnedAccessories);
    }

    [Fact]
    public void コレクションがnullなら空として読む()
    {
        var state = Load("""{"Shells":3,"Items":null,"OwnedAccessories":null,"ScatteredItems":null}""");

        Assert.Empty(state.Items);
        Assert.Empty(state.OwnedAccessories);
        Assert.Empty(state.ScatteredItems);
        Assert.Equal(3, state.Shells);
    }
}

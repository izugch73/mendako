using System.Text.Json;
using Mendako.Core.Model;
using Xunit;

namespace Mendako.Core.Tests;

/// <summary>アイテムの一覧と、保存データの読み書きのテスト。</summary>
public class ItemCatalogTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void アイテムは100種類で_識別子と行列が重ならない()
    {
        Assert.Equal(100, ItemCatalog.All.Count);
        Assert.Equal(100, ItemCatalog.All.Select(d => d.Id).Distinct().Count());
        Assert.Equal(100, ItemCatalog.All.Select(d => (d.Row, d.Column)).Distinct().Count());
        Assert.All(ItemCatalog.All, d =>
        {
            Assert.InRange(d.Row, 1, 10);
            Assert.InRange(d.Column, 1, 10);
            Assert.False(string.IsNullOrWhiteSpace(d.DisplayName));
        });
    }

    [Fact]
    public void すべての識別子が一覧に定義されている()
    {
        foreach (var id in Enum.GetValues<ItemId>())
        {
            Assert.Equal(id, ItemCatalog.Get(id).Id);
        }
    }

    [Theory]
    [InlineData(ItemCategory.Food, ItemFrequency.Common, 1, 3)]
    [InlineData(ItemCategory.Material, ItemFrequency.Common, 2, 5)]
    [InlineData(ItemCategory.Gear, ItemFrequency.Occasional, 8, 20)]
    [InlineData(ItemCategory.Treasure, ItemFrequency.Rare, 30, 100)]
    public void 分類ごとの頻度と売値の範囲を守る(ItemCategory category, ItemFrequency frequency, int min, int max)
    {
        var items = ItemCatalog.All.Where(d => d.Category == category).ToList();

        Assert.NotEmpty(items);
        Assert.All(items, d =>
        {
            Assert.Equal(frequency, d.Frequency);
            Assert.InRange(d.SellPrice, min, max);
        });
    }

    // --- 保存 ---

    [Fact]
    public void 新しい項目の無い古い保存データも既定値で読める()
    {
        const string old = """
            {
              "SchemaVersion": 1,
              "Name": "めんちゃん",
              "BornAtUtc": "2026-05-01T12:00:00+00:00",
              "LastTickUtc": "2026-06-01T12:00:00+00:00",
              "LivedMinutes": 44640,
              "Satiety": 55,
              "Energy": 66,
              "Affection": 77,
              "Growth": 46000,
              "IsAsleep": false,
              "LastFedUtc": null,
              "LastPettedUtc": null,
              "FeedCount": 3,
              "PetCount": 4,
              "LastSkippedMinutes": 0
            }
            """;

        var state = JsonSerializer.Deserialize<MendakoState>(old)!;

        Assert.Equal(1, state.SchemaVersion);
        Assert.Equal(46000d, state.Growth);
        Assert.Equal(GrowthStage.Elder, state.Stage);
        Assert.Equal(0, state.Shells);
        Assert.Empty(state.Items);
        Assert.Empty(state.OwnedAccessories);
        Assert.Equal(Accessory.None, state.EquippedAccessory);
        Assert.Null(state.LastForageUtc);

        // 読み込んだあとそのまま進めても動く
        var outcome = Simulation.Simulator.Advance(
            state, state.LastTickUtc.AddHours(3), TimeZoneInfo.Utc, random: new Random(1));
        Assert.Single(outcome.Foraged);
    }

    [Fact]
    public void 持ち物とかいがらと飾りは保存して読み戻せる()
    {
        var state = MendakoState.CreateNew(Noon) with
        {
            Shells = 123,
            Items = new[] { ItemId.RedPotion, ItemId.Apple, ItemId.Apple },
            LastForageUtc = Noon.AddHours(3),
            OwnedAccessories = new[] { Accessory.Flower, Accessory.TopHat },
            EquippedAccessory = Accessory.TopHat,
        };

        var json = JsonSerializer.Serialize(state);
        var back = JsonSerializer.Deserialize<MendakoState>(json)!;

        // アイテムは数値ではなく名前で保存される (一覧の並べ替えで中身が入れ替わらない)
        Assert.Contains("\"RedPotion\"", json);
        Assert.Equal(123, back.Shells);
        Assert.Equal(state.Items, back.Items);
        Assert.Equal(state.LastForageUtc, back.LastForageUtc);
        Assert.Equal(state.OwnedAccessories, back.OwnedAccessories);
        Assert.Equal(Accessory.TopHat, back.EquippedAccessory);
    }
}

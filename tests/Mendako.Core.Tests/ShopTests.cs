using Mendako.Core.Model;
using Mendako.Core.Simulation;
using Xunit;

namespace Mendako.Core.Tests;

/// <summary>売る・買う・付け外しのテスト。</summary>
public class ShopTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static MendakoState Fresh() => MendakoState.CreateNew(Noon);

    // --- 売る ---

    [Fact]
    public void 売ると売値ぶんのかいがらが増えて持ち物から1個減る()
    {
        var state = Fresh() with { Items = new[] { ItemId.Apple, ItemId.Diamond, ItemId.Apple } };

        var outcome = Inventory.Sell(state, ItemId.Apple);

        Assert.Equal(SellResult.Sold, outcome.Result);
        Assert.Equal(ItemCatalog.Get(ItemId.Apple).SellPrice, outcome.ShellsGained);
        Assert.Equal(outcome.ShellsGained, outcome.State.Shells);
        Assert.Equal(new[] { ItemId.Diamond, ItemId.Apple }, outcome.State.Items);
        Assert.True(outcome.Changed);
    }

    [Fact]
    public void 宝物は高く売れる()
    {
        var state = Fresh() with { Items = new[] { ItemId.Diamond }, Shells = 5 };

        var outcome = Inventory.Sell(state, ItemId.Diamond);

        Assert.Equal(105, outcome.State.Shells);
    }

    [Fact]
    public void 持っていないアイテムは売れない()
    {
        var state = Fresh() with { Items = new[] { ItemId.Apple }, Shells = 5 };

        var outcome = Inventory.Sell(state, ItemId.Diamond);

        Assert.Equal(SellResult.NotInInventory, outcome.Result);
        Assert.Equal(0, outcome.ShellsGained);
        Assert.Same(state, outcome.State);
        Assert.False(outcome.Changed);
    }

    // --- 買う ---

    [Fact]
    public void かいがらが足りれば飾りを買えて_持っている飾りに加わる()
    {
        var price = Shop.PriceOf(Accessory.Flower)!.Value;
        var state = Fresh() with { Shells = price + 40 };

        var outcome = Shop.Buy(state, Accessory.Flower);

        Assert.Equal(PurchaseResult.Bought, outcome.Result);
        Assert.Equal(40, outcome.State.Shells);
        Assert.Equal(new[] { Accessory.Flower }, outcome.State.OwnedAccessories);
        Assert.Equal(Accessory.None, outcome.State.EquippedAccessory);
    }

    [Fact]
    public void ちょうどの金額でも買える()
    {
        var price = Shop.PriceOf(Accessory.TopHat)!.Value;
        var state = Fresh() with { Shells = price };

        var outcome = Shop.Buy(state, Accessory.TopHat);

        Assert.Equal(PurchaseResult.Bought, outcome.Result);
        Assert.Equal(0, outcome.State.Shells);
    }

    [Fact]
    public void かいがらが足りないと買えず_不足額がわかる()
    {
        var price = Shop.PriceOf(Accessory.PearlEarrings)!.Value;
        var state = Fresh() with { Shells = price - 1 };

        var outcome = Shop.Buy(state, Accessory.PearlEarrings);

        Assert.Equal(PurchaseResult.NotEnoughShells, outcome.Result);
        Assert.Equal(1, outcome.ShellsShort);
        Assert.Same(state, outcome.State);
        Assert.False(outcome.Changed);
    }

    [Fact]
    public void 同じ飾りは2度買えない()
    {
        var state = Fresh() with { Shells = 1000 };
        var once = Shop.Buy(state, Accessory.StrawHat).State;

        var twice = Shop.Buy(once, Accessory.StrawHat);

        Assert.Equal(PurchaseResult.AlreadyOwned, twice.Result);
        Assert.Equal(once.Shells, twice.State.Shells);
        Assert.Single(twice.State.OwnedAccessories);
    }

    [Fact]
    public void 飾りなしは売っていない()
    {
        var outcome = Shop.Buy(Fresh() with { Shells = 1000 }, Accessory.None);

        Assert.Equal(PurchaseResult.NotForSale, outcome.Result);
    }

    [Fact]
    public void 売っている飾りは5種で_値段は飾りごとに違う()
    {
        var prices = Enum.GetValues<Accessory>()
            .Where(a => a != Accessory.None)
            .Select(a => Shop.PriceOf(a))
            .ToList();

        Assert.Equal(5, prices.Count);
        Assert.Equal(5, prices.Distinct().Count());
    }

    // --- 付け外し ---

    [Fact]
    public void 持っていない飾りは付けられない()
    {
        var state = Fresh();

        var outcome = Shop.Equip(state, Accessory.TopHat);

        Assert.Equal(EquipResult.NotOwned, outcome.Result);
        Assert.Same(state, outcome.State);
    }

    [Fact]
    public void 持っている飾りは付け替えられて_外せる()
    {
        var state = Fresh() with
        {
            OwnedAccessories = new[] { Accessory.Flower, Accessory.TopHat },
        };

        var flower = Shop.Equip(state, Accessory.Flower);
        Assert.Equal(EquipResult.Equipped, flower.Result);
        Assert.Equal(Accessory.Flower, flower.State.EquippedAccessory);

        var hat = Shop.Equip(flower.State, Accessory.TopHat);
        Assert.Equal(Accessory.TopHat, hat.State.EquippedAccessory);

        var none = Shop.Equip(hat.State, Accessory.None);
        Assert.Equal(EquipResult.Unequipped, none.Result);
        Assert.Equal(Accessory.None, none.State.EquippedAccessory);
        Assert.Equal(2, none.State.OwnedAccessories.Count);
    }

    [Fact]
    public void すでに付けている飾りを付けても変わらない()
    {
        var state = Fresh() with
        {
            OwnedAccessories = new[] { Accessory.Flower },
            EquippedAccessory = Accessory.Flower,
        };

        var outcome = Shop.Equip(state, Accessory.Flower);

        Assert.Equal(EquipResult.AlreadyEquipped, outcome.Result);
        Assert.False(outcome.Changed);
    }

    [Fact]
    public void 何も付けていないときに外しても変わらない()
    {
        var outcome = Shop.Equip(Fresh(), Accessory.None);

        Assert.Equal(EquipResult.AlreadyEquipped, outcome.Result);
    }
}

using Mendako.Core.Model;

namespace Mendako.Core.Simulation;

/// <summary>持ち物の操作。</summary>
public static class Inventory
{
    /// <summary>持ち物のアイテムを 1 個売って、売値ぶんのかいがらを得る。</summary>
    public static SellOutcome Sell(MendakoState state, ItemId item)
    {
        ArgumentNullException.ThrowIfNull(state);

        var remaining = state.Items.ToList();
        var index = remaining.IndexOf(item);
        if (index < 0)
        {
            return new SellOutcome(state, SellResult.NotInInventory, item, 0);
        }

        var price = ItemCatalog.Get(item).SellPrice;
        remaining.RemoveAt(index);

        var next = state with { Items = remaining, Shells = state.Shells + price };
        return new SellOutcome(next, SellResult.Sold, item, price);
    }
}

using Mendako.Core.Model;

namespace Mendako.Core.Simulation;

/// <summary>持ち物の操作。</summary>
public static class Inventory
{
    /// <summary>持ち物のアイテムを 1 個売って、売値ぶんのかいがらを得る。</summary>
    public static SellOutcome Sell(MendakoState state, ItemId item)
    {
        ArgumentNullException.ThrowIfNull(state);

        var index = IndexOf(state.Items, item);
        if (index < 0)
        {
            return new SellOutcome(state, SellResult.NotInInventory, item, 0);
        }

        var price = ItemCatalog.Get(item).SellPrice;
        var remaining = state.Items.ToList();
        remaining.RemoveAt(index);

        var next = state with { Items = remaining, Shells = state.Shells + price };
        return new SellOutcome(next, SellResult.Sold, item, price);
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

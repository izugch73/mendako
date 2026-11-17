using Mendako.Core.Model;

namespace Mendako.Core.Simulation;

/// <summary>おみせ。かいがらで飾りを買い、持っている飾りを付け外しする。</summary>
public static class Shop
{
    /// <summary>飾りの値段。売っていなければ null。「飾りなし」は設定に値段があっても売らない。</summary>
    public static int? PriceOf(Accessory accessory, SimulationConfig? config = null)
    {
        if (accessory == Accessory.None)
        {
            return null;
        }

        config ??= SimulationConfig.Default;
        return config.AccessoryPrices.TryGetValue(accessory, out var price) ? price : null;
    }

    /// <summary>飾りを買う。買っただけでは付けない。</summary>
    public static PurchaseOutcome Buy(MendakoState state, Accessory accessory, SimulationConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (PriceOf(accessory, config) is not { } price)
        {
            return new PurchaseOutcome(state, PurchaseResult.NotForSale, accessory, 0);
        }

        if (state.OwnedAccessories.Contains(accessory))
        {
            return new PurchaseOutcome(state, PurchaseResult.AlreadyOwned, accessory, price);
        }

        if (state.Shells < price)
        {
            return new PurchaseOutcome(state, PurchaseResult.NotEnoughShells, accessory, price);
        }

        var next = state with
        {
            Shells = state.Shells - price,
            OwnedAccessories = state.OwnedAccessories.Append(accessory).ToArray(),
        };
        return new PurchaseOutcome(next, PurchaseResult.Bought, accessory, price);
    }

    /// <summary>持っている飾りを付ける。<see cref="Accessory.None"/> を渡すと外す。</summary>
    public static EquipOutcome Equip(MendakoState state, Accessory accessory)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (accessory != Accessory.None && !state.OwnedAccessories.Contains(accessory))
        {
            return new EquipOutcome(state, EquipResult.NotOwned);
        }

        if (state.EquippedAccessory == accessory)
        {
            return new EquipOutcome(state, EquipResult.AlreadyEquipped);
        }

        var result = accessory == Accessory.None ? EquipResult.Unequipped : EquipResult.Equipped;
        return new EquipOutcome(state with { EquippedAccessory = accessory }, result);
    }
}

using Mendako.Core.Model;

namespace Mendako.Core.Simulation;

/// <summary>お世話アクションの結果。</summary>
public enum CareResult
{
    /// <summary>受け入れられた。</summary>
    Accepted,

    /// <summary>反応はするが、クールダウン中なのでパラメータは上がらなかった。</summary>
    AcceptedWithoutGain,

    /// <summary>おなかいっぱいで断られた。</summary>
    RefusedTooFull,

    /// <summary>寝ているので断られた。</summary>
    RefusedAsleep,
}

/// <summary>お世話アクションの戻り値。</summary>
public sealed record CareOutcome(MendakoState State, CareResult Result)
{
    public bool Changed => Result is CareResult.Accepted or CareResult.AcceptedWithoutGain;
}

/// <summary>時間経過シミュレーションの戻り値。</summary>
public sealed record AdvanceOutcome(MendakoState State, long TicksApplied, long TicksSkipped)
{
    /// <summary>このシミュレーションで成長段階が上がったなら、その到達段階。</summary>
    public GrowthStage? StageAdvancedTo { get; init; }

    /// <summary>このシミュレーション中に眠りについたか。</summary>
    public bool FellAsleep { get; init; }

    /// <summary>このシミュレーション中に目を覚ましたか。</summary>
    public bool WokeUp { get; init; }

    /// <summary>このシミュレーションで拾ったアイテム。拾った順。</summary>
    public IReadOnlyList<ForagedItem> Foraged { get; init; } = Array.Empty<ForagedItem>();

    /// <summary>拾う時刻になったが、持ち物がいっぱいで拾えなかった回数。</summary>
    public int ForageMissed { get; init; }
}

/// <summary>拾ったアイテム 1 個。</summary>
public sealed record ForagedItem(ItemId Item, DateTimeOffset AtUtc);

/// <summary>アイテムを売った結果。</summary>
public enum SellResult
{
    /// <summary>売れた。</summary>
    Sold,

    /// <summary>持ち物にないアイテムだった。</summary>
    NotInInventory,
}

/// <summary>アイテムを売ったときの戻り値。</summary>
/// <param name="ShellsGained">得たかいがら。売れなかったときは 0。</param>
public sealed record SellOutcome(MendakoState State, SellResult Result, ItemId Item, int ShellsGained)
{
    public bool Changed => Result == SellResult.Sold;
}

/// <summary>飾りを買った結果。</summary>
public enum PurchaseResult
{
    /// <summary>買えた。</summary>
    Bought,

    /// <summary>すでに持っている。</summary>
    AlreadyOwned,

    /// <summary>かいがらが足りない。</summary>
    NotEnoughShells,

    /// <summary>売っていない (飾りなしなど)。</summary>
    NotForSale,
}

/// <summary>飾りを買ったときの戻り値。</summary>
/// <param name="Price">値段。売っていないときは 0。</param>
public sealed record PurchaseOutcome(MendakoState State, PurchaseResult Result, Accessory Accessory, int Price)
{
    public bool Changed => Result == PurchaseResult.Bought;

    /// <summary>足りなかったかいがら。足りているとき・買えないときは 0。</summary>
    public int ShellsShort => Result == PurchaseResult.NotEnoughShells ? Price - State.Shells : 0;
}

/// <summary>飾りを付け外しした結果。</summary>
public enum EquipResult
{
    /// <summary>付けた。</summary>
    Equipped,

    /// <summary>外した。</summary>
    Unequipped,

    /// <summary>すでにその状態だった。</summary>
    AlreadyEquipped,

    /// <summary>持っていない飾りは付けられない。</summary>
    NotOwned,
}

/// <summary>飾りを付け外ししたときの戻り値。</summary>
public sealed record EquipOutcome(MendakoState State, EquipResult Result)
{
    public bool Changed => Result is EquipResult.Equipped or EquipResult.Unequipped;
}

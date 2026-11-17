using Mendako.Core.Model;

namespace Mendako.Core.Simulation;

/// <summary>アイテムを置く・片付けた結果。</summary>
public enum ScatterResult
{
    /// <summary>置けた。</summary>
    Placed,

    /// <summary>持ち物に戻せた。</summary>
    PickedUp,

    /// <summary>持ち物にないアイテムだった。</summary>
    NotInInventory,

    /// <summary>もう置ける数まで置いてある。</summary>
    TooMany,

    /// <summary>持ち物がいっぱいで戻せなかった。アイテムは置いたまま。</summary>
    InventoryFull,

    /// <summary>置いてあるアイテムの中になかった (すでに片付けた・食べられた)。</summary>
    NotFound,
}

/// <summary>アイテムを置く・片付けたときの戻り値。</summary>
/// <param name="Item">対象の置いてあるアイテム。分からないときは null。</param>
public sealed record ScatterOutcome(MendakoState State, ScatterResult Result, ScatteredItem? Item)
{
    public bool Changed => Result is ScatterResult.Placed or ScatterResult.PickedUp;
}

/// <summary>メンダコが置いてあるアイテムに近寄ったときの反応。</summary>
public enum ScatterReaction
{
    /// <summary>何もしない (すでに無い・寝ている)。</summary>
    None,

    /// <summary>食べた。アイテムは無くなった。</summary>
    Ate,

    /// <summary>食べ物だが、おなかいっぱいで食べなかった。アイテムは残る。</summary>
    RefusedTooFull,

    /// <summary>素材を見て喜んだ。</summary>
    Delighted,

    /// <summary>宝物のきらきらを見て喜んだ。</summary>
    Admired,

    /// <summary>道具をつついて、びっくりした。</summary>
    Poked,
}

/// <summary>置いてあるアイテムへの反応の戻り値。</summary>
public sealed record ScatterReactionOutcome(MendakoState State, ScatterReaction Reaction, ScatteredItem? Item)
{
    public bool Changed => Reaction == ScatterReaction.Ate;
}

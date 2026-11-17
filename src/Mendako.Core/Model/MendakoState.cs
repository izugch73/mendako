using System.Text.Json.Serialization;

namespace Mendako.Core.Model;

/// <summary>
/// 永続化されるメンダコの全状態。イミュータブルで、<see cref="Simulation.Simulator"/> が
/// 新しいインスタンスを返す形でのみ変化する。
/// </summary>
public sealed record MendakoState
{
    /// <summary>保存フォーマットのバージョン。将来のマイグレーション用。</summary>
    public int SchemaVersion { get; init; } = 1;

    public string Name { get; init; } = "めんちゃん";

    public DateTimeOffset BornAtUtc { get; init; }

    /// <summary>最後にシミュレーションを進めた時刻。オフライン進行の起点。</summary>
    public DateTimeOffset LastTickUtc { get; init; }

    /// <summary>シミュレーション上で生きた分数 (キャッチアップ上限で切り捨てられることがある)。</summary>
    public long LivedMinutes { get; init; }

    /// <summary>満腹度 0-100。</summary>
    public double Satiety { get; init; } = 70d;

    /// <summary>元気 0-100。眠ると回復する。</summary>
    public double Energy { get; init; } = 80d;

    /// <summary>なつき度 0-100。世話をすると上がり、放置すると少しずつ下がる。</summary>
    public double Affection { get; init; } = 10d;

    /// <summary>累積成長ポイント。決して減らない。</summary>
    public double Growth { get; init; }

    public bool IsAsleep { get; init; }

    public DateTimeOffset? LastFedUtc { get; init; }

    public DateTimeOffset? LastPettedUtc { get; init; }

    public int FeedCount { get; init; }

    public int PetCount { get; init; }

    /// <summary>
    /// 直近のキャッチアップで打ち切られた分数。0 より大きければ長期間ほったらかしだったことを意味し、
    /// UI 側で「おかえり」演出に使える。
    /// </summary>
    public long LastSkippedMinutes { get; init; }

    /// <summary>かいがら (お金)。</summary>
    public int Shells { get; init; }

    /// <summary>持ち物。同じアイテムも 1 個ずつ並べる。新しい項目なので、無い保存データは空として読む。</summary>
    [JsonConverter(typeof(ItemIdListConverter))]
    public IReadOnlyList<ItemId> Items { get; init; } = Array.Empty<ItemId>();

    /// <summary>
    /// 最後に拾う機会があった時刻。持ち物がいっぱいで拾えなかった分も含む。
    /// ぬしになった時刻から数え始め、次に拾う時刻はここから <see cref="Simulation.SimulationConfig.ForageInterval"/> 後になる。
    /// </summary>
    public DateTimeOffset? LastForageUtc { get; init; }

    /// <summary>持っている飾り。<see cref="Accessory.None"/> は含めない。</summary>
    [JsonConverter(typeof(AccessoryListConverter))]
    public IReadOnlyList<Accessory> OwnedAccessories { get; init; } = Array.Empty<Accessory>();

    /// <summary>付けている飾り。持っている飾りの中から選ぶ。</summary>
    public Accessory EquippedAccessory { get; init; } = Accessory.None;

    /// <summary>タスクバーに置いてあるアイテム。新しい項目なので、無い保存データは空として読む。</summary>
    [JsonConverter(typeof(ScatteredItemListConverter))]
    public IReadOnlyList<ScatteredItem> ScatteredItems { get; init; } = Array.Empty<ScatteredItem>();

    [JsonIgnore]
    public GrowthStage Stage => GrowthStages.FromGrowth(Growth);

    [JsonIgnore]
    public double StageProgress => GrowthStages.ProgressToNext(Growth);

    [JsonIgnore]
    public Mood Mood => Moods.Resolve(Satiety, Energy, Affection);

    /// <summary>新規に飼い始めるときの初期状態。</summary>
    public static MendakoState CreateNew(DateTimeOffset nowUtc, string? name = null) => new()
    {
        Name = string.IsNullOrWhiteSpace(name) ? "めんちゃん" : name.Trim(),
        BornAtUtc = nowUtc,
        LastTickUtc = nowUtc,
    };
}

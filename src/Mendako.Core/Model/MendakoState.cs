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

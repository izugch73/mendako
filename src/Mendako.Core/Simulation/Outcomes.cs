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
}

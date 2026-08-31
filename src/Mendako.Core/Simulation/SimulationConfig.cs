namespace Mendako.Core.Simulation;

/// <summary>
/// 育成バランスの調整値。すべて 1 ティック (= 1 分) あたりの量で表す。
/// テストからは値を差し替えて極端な条件を再現できる。
/// </summary>
public sealed record SimulationConfig
{
    public static readonly SimulationConfig Default = new();

    /// <summary>1 ティックの実時間。</summary>
    public TimeSpan TickInterval { get; init; } = TimeSpan.FromMinutes(1);

    // --- 満腹度 ---

    /// <summary>起きているときの満腹度の減少量。100 -> 0 でおよそ 24 時間。</summary>
    public double SatietyDecayPerTick { get; init; } = 100d / (24 * 60);

    /// <summary>睡眠中の満腹度減少にかかる倍率。</summary>
    public double SleepSatietyDecayMultiplier { get; init; } = 0.5d;

    // --- 元気 ---

    /// <summary>起きているときの元気の減少量。100 -> 0 でおよそ 16 時間。</summary>
    public double EnergyDecayPerTick { get; init; } = 100d / (16 * 60);

    /// <summary>睡眠中の元気の回復量。0 -> 100 でおよそ 6.7 時間。</summary>
    public double EnergyRecoveryPerTick { get; init; } = 0.25d;

    // --- なつき度 ---

    /// <summary>放置によるなつき度の減少量。100 -> 0 でおよそ 20 日。</summary>
    public double AffectionDecayPerTick { get; init; } = 100d / (20 * 24 * 60);

    // --- 成長 ---

    /// <summary>理想的な状態での 1 ティックあたりの成長ポイント。</summary>
    public double MaxGrowthPerTick { get; init; } = 1.0d;

    /// <summary>この満腹度以下では成長しない。</summary>
    public double GrowthSatietyFloor { get; init; } = 25d;

    /// <summary>睡眠中の成長にかかる倍率。寝る子は育つので 1.0 に近い値にしてある。</summary>
    public double SleepGrowthMultiplier { get; init; } = 0.9d;

    // --- 睡眠 ---

    /// <summary>夜とみなす開始時刻 (ローカル時間、0-23)。</summary>
    public int SleepStartHour { get; init; } = 23;

    /// <summary>夜が明けるとみなす時刻 (ローカル時間、0-23)。</summary>
    public int SleepEndHour { get; init; } = 7;

    /// <summary>夜にこの元気を下回ると眠りにつく。</summary>
    public double NightSleepEnergy { get; init; } = 70d;

    /// <summary>時間帯に関係なく、この元気を下回ると力尽きて眠る。</summary>
    public double ForcedSleepEnergy { get; init; } = 15d;

    /// <summary>朝以降、この元気まで回復したら起きる。</summary>
    public double WakeEnergy { get; init; } = 60d;

    // --- オフライン進行 ---

    /// <summary>
    /// 起動時にまとめて進めるティック数の上限。既定は 3 日分。
    /// 旅行から帰ってきたら手遅れ、という体験を避けるための安全弁。
    /// </summary>
    public long MaxCatchUpTicks { get; init; } = 3 * 24 * 60;

    // --- お世話 ---

    public double FeedSatietyGain { get; init; } = 35d;

    public double FeedAffectionGain { get; init; } = 2d;

    /// <summary>この満腹度以上ではエサを食べない。</summary>
    public double FeedRefuseThreshold { get; init; } = 90d;

    public double PetAffectionGain { get; init; } = 1.5d;

    /// <summary>なでてもなつき度が上がらなくなるクールダウン。</summary>
    public TimeSpan PetCooldown { get; init; } = TimeSpan.FromMinutes(10);
}

namespace Mendako.Core.Model;

/// <summary>メンダコの成長段階。</summary>
public enum GrowthStage
{
    /// <summary>たまご。まだ孵っていない。</summary>
    Egg = 0,

    /// <summary>稚メンダコ。</summary>
    Hatchling = 1,

    /// <summary>若メンダコ。</summary>
    Juvenile = 2,

    /// <summary>メンダコ。</summary>
    Adult = 3,

    /// <summary>ぬしメンダコ。</summary>
    Elder = 4,
}

public static class GrowthStages
{
    /// <summary>各段階に到達するのに必要な累積成長ポイント。</summary>
    /// <remarks>
    /// 1 ティック (= 1 分) の理想的な世話で最大 1.0 ポイント入るので、
    /// Elder までは「完璧な世話でおよそ 31 日」という目安になる。
    /// </remarks>
    public static readonly IReadOnlyList<double> Thresholds = new[]
    {
        0d,      // Egg
        600d,    // Hatchling  (約 10 時間)
        4_000d,  // Juvenile   (約 3 日)
        15_000d, // Adult      (約 10 日)
        45_000d, // Elder      (約 31 日)
    };

    public static GrowthStage FromGrowth(double growth)
    {
        var stage = GrowthStage.Egg;
        for (var i = 0; i < Thresholds.Count; i++)
        {
            if (growth >= Thresholds[i])
            {
                stage = (GrowthStage)i;
            }
        }

        return stage;
    }

    /// <summary>次の段階までの進捗を 0.0 - 1.0 で返す。最終段階なら 1.0。</summary>
    public static double ProgressToNext(double growth)
    {
        var stage = FromGrowth(growth);
        var index = (int)stage;
        if (index >= Thresholds.Count - 1)
        {
            return 1d;
        }

        var floor = Thresholds[index];
        var ceiling = Thresholds[index + 1];
        var progress = (growth - floor) / (ceiling - floor);
        return Math.Clamp(progress, 0d, 1d);
    }

    public static string DisplayName(GrowthStage stage) => stage switch
    {
        GrowthStage.Egg => "たまご",
        GrowthStage.Hatchling => "稚メンダコ",
        GrowthStage.Juvenile => "若メンダコ",
        GrowthStage.Adult => "メンダコ",
        GrowthStage.Elder => "ぬしメンダコ",
        _ => "???",
    };
}

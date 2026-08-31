using Mendako.Core.Model;
using Xunit;

namespace Mendako.Core.Tests;

public class GrowthStagesTests
{
    [Theory]
    [InlineData(0, GrowthStage.Egg)]
    [InlineData(599, GrowthStage.Egg)]
    [InlineData(600, GrowthStage.Hatchling)]
    [InlineData(3_999, GrowthStage.Hatchling)]
    [InlineData(4_000, GrowthStage.Juvenile)]
    [InlineData(15_000, GrowthStage.Adult)]
    [InlineData(45_000, GrowthStage.Elder)]
    [InlineData(999_999, GrowthStage.Elder)]
    public void 累積成長ポイントから段階が決まる(double growth, GrowthStage expected)
    {
        Assert.Equal(expected, GrowthStages.FromGrowth(growth));
    }

    [Fact]
    public void 段階の途中では進捗が0と1のあいだになる()
    {
        var progress = GrowthStages.ProgressToNext(300d);

        Assert.InRange(progress, 0.4d, 0.6d);
    }

    [Fact]
    public void 最終段階では進捗は1になる()
    {
        Assert.Equal(1d, GrowthStages.ProgressToNext(100_000d));
    }

    [Theory]
    [InlineData(0d, 100d, 10d, Mood.Gloomy)]
    [InlineData(10d, 100d, 100d, Mood.Hungry)]
    [InlineData(80d, 10d, 100d, Mood.Sleepy)]
    [InlineData(80d, 80d, 10d, Mood.Lonely)]
    [InlineData(90d, 90d, 90d, Mood.Happy)]
    [InlineData(40d, 40d, 40d, Mood.Content)]
    public void パラメータから気分が決まる(double satiety, double energy, double affection, Mood expected)
    {
        Assert.Equal(expected, Moods.Resolve(satiety, energy, affection));
    }
}

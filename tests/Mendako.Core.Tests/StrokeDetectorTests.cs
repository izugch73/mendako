using Mendako.Core.Behavior;
using Xunit;

namespace Mendako.Core.Tests;

public class StrokeDetectorTests
{
    private const double Frame = 1d / 30d;

    /// <summary>
    /// 折れ線に沿ってカーソルを動かし、なでたと判定された回数を返す。
    /// 各区間は <paramref name="secondsPerLeg"/> 秒かけて等速で進む。
    /// </summary>
    private static int Trace(StrokeDetector detector, double secondsPerLeg, params double[] waypoints) =>
        TraceFrom(detector, 0d, secondsPerLeg, waypoints).Count;

    private static (int Count, double Time) TraceFrom(
        StrokeDetector detector,
        double startTime,
        double secondsPerLeg,
        params double[] waypoints)
    {
        var time = startTime;
        var count = 0;

        for (var i = 0; i + 1 < waypoints.Length; i++)
        {
            var steps = Math.Max(1, (int)Math.Round(secondsPerLeg / Frame));
            for (var s = 0; s <= steps; s++)
            {
                var x = waypoints[i] + ((waypoints[i + 1] - waypoints[i]) * s / steps);
                if (detector.Update(time, x, overPet: true))
                {
                    count++;
                }

                time += Frame;
            }
        }

        return (count, time);
    }

    [Fact]
    public void 左右に往復するとなでたことになる()
    {
        var count = Trace(new StrokeDetector(), 0.2d, 0d, 30d, 0d, 30d);

        Assert.Equal(1, count);
    }

    [Fact]
    public void 通り過ぎただけではなでたことにならない()
    {
        var count = Trace(new StrokeDetector(), 0.3d, 0d, 100d);

        Assert.Equal(0, count);
    }

    [Fact]
    public void 一往復では足りない()
    {
        var count = Trace(new StrokeDetector(), 0.2d, 0d, 30d, 0d);

        Assert.Equal(0, count);
    }

    [Fact]
    public void 手ぶれ程度の揺れは数えない()
    {
        var count = Trace(new StrokeDetector(), 0.1d, 0d, 3d, 0d, 3d, 0d, 3d, 0d, 3d);

        Assert.Equal(0, count);
    }

    [Fact]
    public void ゆっくりすぎる往復は数えない()
    {
        // 折り返しのあいだが空きすぎる
        var count = Trace(new StrokeDetector(), 1.5d, 0d, 30d, 0d, 30d, 0d);

        Assert.Equal(0, count);
    }

    [Fact]
    public void なで続けても連射にはならない()
    {
        // 0.15 秒 × 20 区間 = 3 秒。判定のあと 1.2 秒は数えないので、多くても 2 回
        var waypoints = Enumerable.Range(0, 21).Select(i => i % 2 == 0 ? 0d : 30d).ToArray();

        var count = Trace(new StrokeDetector(), 0.15d, waypoints);

        Assert.InRange(count, 1, 2);
    }

    [Fact]
    public void 体の外に出ると数え直しになる()
    {
        var detector = new StrokeDetector();

        var (first, time) = TraceFrom(detector, 0d, 0.2d, 0d, 30d, 0d);
        detector.Update(time, 0d, overPet: false);
        var (second, _) = TraceFrom(detector, time + Frame, 0.2d, 0d, 30d);

        Assert.Equal(0, first + second);
    }

    [Fact]
    public void 体の外での往復は数えない()
    {
        var detector = new StrokeDetector();
        var fired = false;

        for (var i = 0; i < 90; i++)
        {
            fired |= detector.Update(i * Frame, (i / 5) % 2 == 0 ? 0d : 30d, overPet: false);
        }

        Assert.False(fired);
    }
}

using Mendako.Core.Behavior;
using Mendako.Core.Model;
using Xunit;

namespace Mendako.Core.Tests;

/// <summary>
/// コマ選びのテスト。時間は Advance に渡す秒数だけで進むので、
/// まばたきやリアクションの長さを実時間を待たずに検証できる。
/// </summary>
public class BehaviorMachineTests
{
    private const double Frame = 1d / 30d;

    private static readonly DateTimeOffset Noon =
        new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static MendakoState Awake(double satiety = 80d, double energy = 80d, double affection = 80d) =>
        MendakoState.CreateNew(Noon) with { Growth = 20_000d, Satiety = satiety, Energy = energy, Affection = affection };

    private static MendakoState Asleep() => Awake() with { IsAsleep = true };

    /// <summary>指定秒数ぶんフレームを回し、そのあいだのコマをすべて返す。</summary>
    private static List<PetPose> Run(BehaviorMachine machine, MendakoState state, double seconds)
    {
        var poses = new List<PetPose>();
        for (var t = 0d; t < seconds; t += Frame)
        {
            poses.Add(machine.Advance(Frame, state));
        }

        return poses;
    }

    // --- 待機 ---

    [Fact]
    public void 待機中は耳ビレがUpとMidを行き来する()
    {
        var machine = new BehaviorMachine(seed: 1);

        var poses = Run(machine, Awake(), 10d);

        Assert.Contains(poses, p => p.Fin == FinPose.Up);
        Assert.Contains(poses, p => p.Fin == FinPose.Mid);
        Assert.DoesNotContain(poses, p => p.Fin == FinPose.Droop);
        Assert.DoesNotContain(poses, p => p.ShowHeart || p.ShowSleepMark);
    }

    [Fact]
    public void 元気がないと耳ビレが垂れる()
    {
        var state = Awake(satiety: 5d, energy: 5d, affection: 5d);
        Assert.True(state.Mood is Mood.Sleepy or Mood.Gloomy, $"前提が崩れている: {state.Mood}");

        var poses = Run(new BehaviorMachine(seed: 1), state, 5d);

        Assert.All(poses, p => Assert.Equal(FinPose.Droop, p.Fin));
    }

    [Fact]
    public void 待機中はまばたきをして_すぐ目を開ける()
    {
        var machine = new BehaviorMachine(seed: 1);

        var poses = Run(machine, Awake(), 30d);

        // 間隔は 2.5〜7 秒なので 30 秒あれば必ず数回は閉じる
        var longestClosed = 0;
        var current = 0;
        var blinks = 0;
        foreach (var pose in poses)
        {
            if (pose.Eyes == EyePose.Closed)
            {
                if (current == 0)
                {
                    blinks++;
                }

                current++;
                longestClosed = Math.Max(longestClosed, current);
            }
            else
            {
                current = 0;
            }
        }

        Assert.InRange(blinks, 4, 12);
        Assert.True(longestClosed * Frame < 0.3d, $"目を閉じている時間が長すぎる: {longestClosed} フレーム");
    }

    [Fact]
    public void 同じシードなら同じコマ列になる()
    {
        var first = Run(new BehaviorMachine(seed: 42), Awake(), 20d);
        var second = Run(new BehaviorMachine(seed: 42), Awake(), 20d);

        Assert.Equal(first, second);
    }

    // --- 睡眠 ---

    [Fact]
    public void 就寝中は目を閉じてzZZを出す()
    {
        var poses = Run(new BehaviorMachine(seed: 1), Asleep(), 5d);

        Assert.All(poses, p =>
        {
            Assert.Equal(EyePose.Closed, p.Eyes);
            Assert.Equal(FinPose.Droop, p.Fin);
            Assert.True(p.ShowSleepMark);
        });
    }

    [Fact]
    public void 就寝中でもリアクションは再生される()
    {
        var machine = new BehaviorMachine(seed: 1);
        machine.Trigger(PetAction.Refuse);

        var pose = machine.Advance(Frame, Asleep());

        Assert.False(pose.ShowSleepMark);
        Assert.Equal(PetAction.Refuse, machine.CurrentAction);
    }

    // --- リアクション ---

    [Fact]
    public void なでるとハートを出してにっこりする()
    {
        var machine = new BehaviorMachine(seed: 1);
        machine.Trigger(PetAction.Happy);

        var poses = Run(machine, Awake(), 1d);

        Assert.All(poses, p =>
        {
            Assert.Equal(EyePose.Happy, p.Eyes);
            Assert.True(p.ShowHeart);
            Assert.True(p.BobDots <= 0d, "喜んでいるときは跳ねる (上方向) だけ");
        });
    }

    [Theory]
    [InlineData(PetAction.Eat, 1.8d)]
    [InlineData(PetAction.Happy, 1.6d)]
    [InlineData(PetAction.Refuse, 0.9d)]
    [InlineData(PetAction.Evolve, 2.6d)]
    public void リアクションは既定の長さで終わって待機に戻る(PetAction action, double duration)
    {
        var machine = new BehaviorMachine(seed: 1);
        machine.Trigger(action);

        machine.Advance(duration - 0.1d, Awake());
        Assert.Equal(action, machine.CurrentAction);

        machine.Advance(0.2d, Awake());
        Assert.Equal(PetAction.None, machine.CurrentAction);
    }

    [Fact]
    public void 長さを指定するとその秒数で終わる()
    {
        var machine = new BehaviorMachine(seed: 1);
        machine.Trigger(PetAction.Happy, durationSeconds: 0.5d);

        machine.Advance(0.6d, Awake());

        Assert.Equal(PetAction.None, machine.CurrentAction);
    }

    [Fact]
    public void 負の経過時間では時間が戻らない()
    {
        var machine = new BehaviorMachine(seed: 1);
        machine.Trigger(PetAction.Happy);
        machine.Advance(1d, Awake());

        machine.Advance(-10d, Awake());
        machine.Advance(0.7d, Awake());

        Assert.Equal(PetAction.None, machine.CurrentAction);
    }

    // --- 優先度 ---

    [Fact]
    public void 進化演出は世話のリアクションで上書きされない()
    {
        var machine = new BehaviorMachine(seed: 1);
        machine.Trigger(PetAction.Evolve);
        machine.Advance(0.5d, Awake());

        // ごはんで段階が上がると、進化の直後に Eat が届く
        Assert.False(machine.Trigger(PetAction.Eat));
        Assert.False(machine.Trigger(PetAction.Happy));
        Assert.False(machine.Trigger(PetAction.Refuse));

        Assert.Equal(PetAction.Evolve, machine.CurrentAction);
    }

    [Fact]
    public void 進化演出は世話のリアクションを上書きする()
    {
        var machine = new BehaviorMachine(seed: 1);
        machine.Trigger(PetAction.Eat);

        Assert.True(machine.Trigger(PetAction.Evolve));
        Assert.Equal(PetAction.Evolve, machine.CurrentAction);
    }

    [Fact]
    public void 同じ優先度なら後から来たほうが勝ち_長さも測り直す()
    {
        var machine = new BehaviorMachine(seed: 1);
        machine.Trigger(PetAction.Eat);
        machine.Advance(1.5d, Awake());

        Assert.True(machine.Trigger(PetAction.Happy));
        machine.Advance(1.5d, Awake());

        // Eat のままなら 1.8 秒で終わっているはず
        Assert.Equal(PetAction.Happy, machine.CurrentAction);
    }

    [Fact]
    public void 進化演出が終わった直後は_Advanceを挟まなくても次を受け付ける()
    {
        var machine = new BehaviorMachine(seed: 1);
        machine.Trigger(PetAction.Evolve, durationSeconds: 0d);

        Assert.True(machine.Trigger(PetAction.Eat));
    }

    [Fact]
    public void Noneは何も起こさない()
    {
        var machine = new BehaviorMachine(seed: 1);
        machine.Trigger(PetAction.Happy);

        Assert.False(machine.Trigger(PetAction.None));
        Assert.Equal(PetAction.Happy, machine.CurrentAction);
    }
}

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

    private static MendakoState Egg() => Awake() with { Growth = 0d };

    /// <summary>左右どちらにも十分に動ける状況。</summary>
    private static readonly BehaviorInput Roomy = new() { RoomBeforeDots = 200d, RoomAfterDots = 200d };

    /// <summary>ひとりでにしぐさを始めない機械。待機そのものを見たいテスト用。</summary>
    private static BehaviorMachine Quiet(int seed = 1) => new(seed) { GesturesEnabled = false };

    /// <summary>指定秒数ぶんフレームを回し、そのあいだのコマをすべて返す。</summary>
    private static List<PetPose> Run(
        BehaviorMachine machine,
        MendakoState state,
        double seconds,
        BehaviorInput input = default)
    {
        var poses = new List<PetPose>();
        for (var t = 0d; t < seconds; t += Frame)
        {
            poses.Add(machine.Advance(Frame, state, input));
        }

        return poses;
    }

    /// <summary>指定秒数のあいだに現れた動きを、現れた順に重複なく返す。</summary>
    private static List<PetAction> ActionsSeen(
        BehaviorMachine machine,
        MendakoState state,
        double seconds,
        BehaviorInput input = default)
    {
        var seen = new List<PetAction>();
        for (var t = 0d; t < seconds; t += Frame)
        {
            machine.Advance(Frame, state, input);
            if (machine.CurrentAction != PetAction.None && !seen.Contains(machine.CurrentAction))
            {
                seen.Add(machine.CurrentAction);
            }
        }

        return seen;
    }

    // --- 待機 ---

    [Fact]
    public void 待機中は耳ビレがUpとMidを行き来する()
    {
        var machine = Quiet();

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

        var poses = Run(Quiet(), state, 5d);

        Assert.All(poses, p => Assert.Equal(FinPose.Droop, p.Fin));
    }

    [Fact]
    public void 待機中はまばたきをして_すぐ目を開ける()
    {
        var machine = Quiet();

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
        var first = Run(new BehaviorMachine(seed: 42), Awake(), 120d, Roomy);
        var second = Run(new BehaviorMachine(seed: 42), Awake(), 120d, Roomy);

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

    [Fact]
    public void 食べているあいだは目を閉じたまま()
    {
        var machine = Quiet();
        machine.Trigger(PetAction.Eat);

        var poses = Run(machine, Awake(), 1.5d);

        Assert.All(poses, p => Assert.Equal(EyePose.Closed, p.Eyes));
    }

    // --- 視線 ---

    [Theory]
    [InlineData(20d, -5d, 1)]
    [InlineData(-20d, -5d, -1)]
    [InlineData(1d, -20d, 0)] // ほぼ真上
    [InlineData(200d, 0d, 0)] // 遠すぎる
    public void 近くのカーソルを目で追う(double x, double y, int expected)
    {
        var input = new BehaviorInput { CursorDots = (x, y) };

        var pose = Quiet().Advance(Frame, Awake(), input);

        Assert.Equal(expected, pose.GazeDots);
    }

    [Fact]
    public void カーソルの位置が分からなければ正面を見る()
    {
        var pose = Quiet().Advance(Frame, Awake());

        Assert.Equal(0, pose.GazeDots);
    }

    // --- しぐさ ---

    [Fact]
    public void 放っておくとひとりでにしぐさをする()
    {
        var seen = ActionsSeen(new BehaviorMachine(seed: 7), Awake(), 300d, Roomy);

        Assert.True(seen.Count >= 3, $"しぐさの種類が少ない: {string.Join(", ", seen)}");
        Assert.DoesNotContain(PetAction.Eat, seen);
        Assert.DoesNotContain(PetAction.Wobble, seen);
    }

    [Fact]
    public void しぐさを切ると待機だけになる()
    {
        var seen = ActionsSeen(Quiet(), Awake(), 300d, Roomy);

        Assert.Empty(seen);
    }

    [Fact]
    public void 就寝中はしぐさをしない()
    {
        var seen = ActionsSeen(new BehaviorMachine(seed: 7), Asleep(), 300d, Roomy);

        Assert.Empty(seen);
    }

    [Fact]
    public void たまごは揺れるだけ()
    {
        var state = Egg();
        Assert.Equal(GrowthStage.Egg, state.Stage);

        var machine = new BehaviorMachine(seed: 7);
        var seen = ActionsSeen(machine, state, 300d, Roomy);

        Assert.Equal(new[] { PetAction.Wobble }, seen);
    }

    [Fact]
    public void ねむいときは泳がず_ぱたぱたもしない()
    {
        var state = Awake(energy: 10d);
        Assert.Equal(Mood.Sleepy, state.Mood);

        var seen = ActionsSeen(new BehaviorMachine(seed: 7), state, 600d, Roomy);

        Assert.Contains(PetAction.Yawn, seen);
        Assert.DoesNotContain(PetAction.Swim, seen);
        Assert.DoesNotContain(PetAction.Flutter, seen);
    }

    [Fact]
    public void きょろきょろは左右を順に見る()
    {
        var machine = Quiet();
        machine.Trigger(PetAction.LookAround);

        var gazes = Run(machine, Awake(), 1.7d).Select(p => p.GazeDots).Distinct().ToList();

        Assert.Equal(new[] { -1, 0, 1 }, gazes.Take(3));
    }

    [Fact]
    public void ぺたんとすると平たいコマになる()
    {
        var machine = Quiet();
        machine.Trigger(PetAction.Flatten);

        var poses = Run(machine, Awake(), 2d);

        Assert.All(poses, p =>
        {
            Assert.True(p.Flat);
            Assert.Equal(0d, p.BobDots);
        });
    }

    [Fact]
    public void しぐさは世話のリアクションに譲る()
    {
        var machine = Quiet();
        machine.Trigger(PetAction.Flatten);

        Assert.True(machine.Trigger(PetAction.Happy));

        // 逆に、喜んでいる最中にしぐさは割り込めない
        Assert.False(machine.Trigger(PetAction.Yawn));
        Assert.Equal(PetAction.Happy, machine.CurrentAction);
    }

    // --- おさんぽ ---

    [Fact]
    public void 動ける余地がなければ泳ぎ出さない()
    {
        // 既定の入力は余地ゼロ
        var seen = ActionsSeen(new BehaviorMachine(seed: 7), Awake(), 600d);

        Assert.DoesNotContain(PetAction.Swim, seen);
    }

    [Fact]
    public void 泳ぐと余地のある側へ進み_余地を超えない()
    {
        var machine = Quiet();
        machine.Trigger(PetAction.Swim, durationSeconds: 60d);

        // 右にだけ 10 ドット動ける
        var room = 10d;
        var travelled = 0d;
        for (var i = 0; i < 60 * 30 && machine.CurrentAction == PetAction.Swim; i++)
        {
            var pose = machine.Advance(Frame, Awake(), new BehaviorInput { RoomAfterDots = room - travelled });
            Assert.True(pose.TravelDots >= 0d, "余地のない左へ進んだ");
            travelled += pose.TravelDots;
        }

        Assert.Equal(room, travelled, precision: 6);

        // 端に着いたら、指定した長さを待たずに切り上げる
        Assert.Equal(PetAction.None, machine.CurrentAction);
    }

    [Fact]
    public void 泳いでいるあいだは進行方向を見る()
    {
        var machine = Quiet();
        machine.Trigger(PetAction.Swim);

        var poses = Run(machine, Awake(), 1d, new BehaviorInput { RoomBeforeDots = 100d });

        Assert.All(poses, p => Assert.True(p.TravelDots <= 0d));
        Assert.Contains(poses, p => p.Eyes == EyePose.Open && p.GazeDots == -1);
        Assert.DoesNotContain(poses, p => p.GazeDots == 1);
    }

    [Fact]
    public void 縦置きのタスクバーでは横目にならない()
    {
        var machine = Quiet();
        machine.Trigger(PetAction.Swim);

        var poses = Run(machine, Awake(), 1d, Roomy with { VerticalRail = true });

        Assert.All(poses, p => Assert.Equal(0, p.GazeDots));
    }

    [Fact]
    public void カーソルを乗せると立ち止まる()
    {
        var machine = Quiet();
        machine.Trigger(PetAction.Swim);
        machine.Advance(0.5d, Awake(), Roomy);

        var pose = machine.Advance(Frame, Awake(), Roomy with { Hovering = true });

        Assert.Equal(PetAction.None, machine.CurrentAction);
        Assert.Equal(0d, pose.TravelDots);
    }

    [Fact]
    public void 泳いでいないときは居場所を動かさない()
    {
        var machine = Quiet();
        foreach (var action in new[] { PetAction.Eat, PetAction.Happy, PetAction.Flutter, PetAction.Yawn })
        {
            machine.Trigger(action, durationSeconds: 1d);
            Assert.All(Run(machine, Awake(), 1.2d, Roomy), p => Assert.Equal(0d, p.TravelDots));
        }
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

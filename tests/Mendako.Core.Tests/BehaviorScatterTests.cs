using Mendako.Core.Behavior;
using Mendako.Core.Model;
using Xunit;

namespace Mendako.Core.Tests;

/// <summary>置いてあるアイテムへ泳いで近寄る動きのテスト。</summary>
public class BehaviorScatterTests
{
    private const double Frame = 1d / 30d;

    private static readonly DateTimeOffset Noon =
        new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static MendakoState Awake() =>
        MendakoState.CreateNew(Noon) with { Growth = 20_000d, Satiety = 80d, Energy = 80d, Affection = 80d };

    private static BehaviorMachine Quiet(int seed = 1) => new(seed) { GesturesEnabled = false };

    /// <summary>メンダコの位置を持って、アイテムまでの距離を毎フレーム測り直す小さな世界。</summary>
    private sealed class World
    {
        private readonly Dictionary<Guid, double> _itemAt = new();

        public World(BehaviorMachine machine, MendakoState? state = null)
        {
            Machine = machine;
            State = state ?? Awake();
        }

        public BehaviorMachine Machine { get; }

        public MendakoState State { get; set; }

        public double PetAt { get; private set; }

        public double RoomBefore { get; set; } = 1000d;

        public double RoomAfter { get; set; } = 1000d;

        public bool Hovering { get; set; }

        public Guid Put(double at)
        {
            var id = Guid.NewGuid();
            _itemAt[id] = at;
            return id;
        }

        public void Remove(Guid id) => _itemAt.Remove(id);

        public double Distance(Guid id) => Math.Abs(_itemAt[id] - PetAt);

        public PetPose Step()
        {
            var input = new BehaviorInput
            {
                Hovering = Hovering,
                RoomBeforeDots = RoomBefore,
                RoomAfterDots = RoomAfter,
                Scattered = _itemAt.Select(pair => new ScatterTarget(pair.Key, pair.Value - PetAt)).ToArray(),
            };

            var pose = Machine.Advance(Frame, State, input);
            PetAt += pose.TravelDots;
            RoomBefore += pose.TravelDots;
            RoomAfter -= pose.TravelDots;
            return pose;
        }

        public List<PetPose> Run(double seconds)
        {
            var poses = new List<PetPose>();
            for (var t = 0d; t < seconds; t += Frame)
            {
                poses.Add(Step());
            }

            return poses;
        }

        /// <summary>着いたと知らされるまで進める。着いたアイテムの識別子と、かかった秒数。</summary>
        public (Guid? Id, double Seconds) RunUntilArrival(double limitSeconds)
        {
            for (var t = 0d; t < limitSeconds; t += Frame)
            {
                Step();
                if (Machine.TakeArrival() is { } id)
                {
                    return (id, t);
                }
            }

            return (null, limitSeconds);
        }
    }

    [Fact]
    public void 右にあるアイテムへ気付いて泳ぎ寄る()
    {
        var world = new World(Quiet());
        var item = world.Put(60d);

        var poses = world.Run(3d);

        Assert.Equal(PetAction.Approach, world.Machine.CurrentAction);
        Assert.All(poses.Where(p => p.TravelDots != 0d), p => Assert.True(p.TravelDots > 0d));
        Assert.True(world.PetAt > 5d);
        Assert.True(world.Distance(item) < 60d);
    }

    [Fact]
    public void 左にあるアイテムへは左へ泳ぐ()
    {
        var world = new World(Quiet());
        world.Put(-60d);

        var poses = world.Run(3d);

        Assert.Equal(PetAction.Approach, world.Machine.CurrentAction);
        Assert.All(poses.Where(p => p.TravelDots != 0d), p => Assert.True(p.TravelDots < 0d));
        Assert.True(world.PetAt < -5d);
        Assert.Contains(poses, p => p.GazeDots == -1);
    }

    [Fact]
    public void 気付いてすぐには泳ぎ出さず少し見る()
    {
        var world = new World(Quiet());
        world.Put(60d);

        var first = world.Run(0.5d);

        Assert.All(first, p => Assert.Equal(0d, p.TravelDots));
        Assert.All(first, p => Assert.Equal(1, p.GazeDots));
        Assert.Equal(PetAction.None, world.Machine.CurrentAction);
    }

    [Fact]
    public void 着いたら止まって一度だけ知らせる()
    {
        var world = new World(Quiet());
        var item = world.Put(60d);

        var (arrived, seconds) = world.RunUntilArrival(60d);

        Assert.Equal(item, arrived);
        Assert.InRange(seconds, 2d, 30d);

        // 近づきすぎて踏まない。体の幅の半分より外側で止まる
        Assert.InRange(world.Distance(item), 9d, 11.5d);

        // 止まっている。もう知らせないし、動かない
        var afterwards = world.Run(5d);
        Assert.Null(world.Machine.TakeArrival());
        Assert.All(afterwards, p => Assert.Equal(0d, p.TravelDots));
        Assert.NotEqual(PetAction.Approach, world.Machine.CurrentAction);
    }

    [Fact]
    public void 最初から着いている距離なら動かずに着いたことにする()
    {
        var world = new World(Quiet());
        var item = world.Put(8d);

        var (arrived, _) = world.RunUntilArrival(5d);

        Assert.Equal(item, arrived);
        Assert.Equal(0d, world.PetAt);
    }

    [Fact]
    public void 着いたあとの反応を引き受けられる()
    {
        var world = new World(Quiet());
        world.Put(30d);
        world.RunUntilArrival(30d);

        // 着いた直後に App が反応を始める。泳ぎに邪魔されずに再生される
        Assert.True(world.Machine.Trigger(PetAction.Eat));
        world.Run(0.5d);

        Assert.Equal(PetAction.Eat, world.Machine.CurrentAction);
    }

    [Fact]
    public void 反応したアイテムにはクールダウン中は近寄らず明けたらまた近寄る()
    {
        var machine = Quiet();
        var state = Awake();
        var id = Guid.NewGuid();

        // 位置を固定した世界で、メンダコ側の移動を無視してクールダウンだけを見る
        BehaviorInput Input(double offset) => new()
        {
            RoomBeforeDots = 1000d,
            RoomAfterDots = 1000d,
            Scattered = new[] { new ScatterTarget(id, offset) },
        };

        // 着く距離の内側にあるアイテムに気付いて、すぐ着く
        Guid? arrived = null;
        for (var t = 0d; t < 5d && arrived is null; t += Frame)
        {
            machine.Advance(Frame, state, Input(5d));
            arrived = machine.TakeArrival();
        }

        Assert.Equal(id, arrived);

        // 同じアイテムが遠くに見える状況に変える
        var swimming = false;
        for (var t = 0d; t < BehaviorMachine.ScatterCooldownSeconds - 2d; t += 0.25d)
        {
            var pose = machine.Advance(0.25d, state, Input(50d));
            swimming |= machine.CurrentAction == PetAction.Approach || pose.TravelDots != 0d;
        }

        Assert.False(swimming);

        // 明けた
        for (var t = 0d; t < 6d; t += 0.25d)
        {
            machine.Advance(0.25d, state, Input(50d));
        }

        Assert.Equal(PetAction.Approach, machine.CurrentAction);
    }

    [Fact]
    public void クールダウン中のアイテムは飛ばして別のアイテムへ向かう()
    {
        var world = new World(Quiet());
        var first = world.Put(30d);
        world.RunUntilArrival(60d);

        // 手前のアイテムは休ませたまま、少し先に別のアイテムを置く
        var second = world.Put(world.PetAt + 70d);

        var (arrived, _) = world.RunUntilArrival(60d);

        Assert.Equal(second, arrived);
        Assert.NotEqual(first, arrived);
    }

    [Fact]
    public void 近くに二つあれば近いほうへ向かう()
    {
        var world = new World(Quiet());
        world.Put(90d);
        var near = world.Put(-40d);

        var (arrived, _) = world.RunUntilArrival(60d);

        Assert.Equal(near, arrived);
    }

    [Fact]
    public void 遠すぎるアイテムには気付かない()
    {
        var world = new World(Quiet());
        world.Put(400d);

        var poses = world.Run(10d);

        Assert.All(poses, p => Assert.Equal(0d, p.TravelDots));
        Assert.Equal(PetAction.None, world.Machine.CurrentAction);
    }

    [Fact]
    public void 寝ているあいだは近寄らない()
    {
        var world = new World(Quiet(), Awake() with { IsAsleep = true });
        world.Put(40d);

        var poses = world.Run(10d);

        Assert.All(poses, p => Assert.Equal(0d, p.TravelDots));
        Assert.All(poses, p => Assert.True(p.ShowSleepMark));
    }

    [Fact]
    public void 泳いでいる途中で眠ったら止まる()
    {
        var world = new World(Quiet());
        world.Put(120d);
        world.Run(3d);
        Assert.Equal(PetAction.Approach, world.Machine.CurrentAction);

        world.State = world.State with { IsAsleep = true };
        var poses = world.Run(1d);

        Assert.All(poses, p => Assert.Equal(0d, p.TravelDots));
        Assert.NotEqual(PetAction.Approach, world.Machine.CurrentAction);
    }

    [Fact]
    public void つままれているあいだは近寄らず放されても落ちきるまで近寄らない()
    {
        var world = new World(Quiet());
        world.Put(60d);
        world.Run(3d);
        Assert.Equal(PetAction.Approach, world.Machine.CurrentAction);

        // つままれた (Held は泳ぎに優先する)
        world.Machine.Trigger(PetAction.Held);
        var held = world.Run(2d);
        Assert.All(held, p => Assert.Equal(0d, p.TravelDots));
        Assert.Equal(PetAction.Held, world.Machine.CurrentAction);

        // 放されて降りている最中も同じ
        world.Machine.Stop(PetAction.Held);
        world.Machine.Trigger(PetAction.Fall);
        var falling = world.Run(2d);
        Assert.All(falling, p => Assert.Equal(0d, p.TravelDots));
        Assert.Equal(PetAction.Fall, world.Machine.CurrentAction);
    }

    [Fact]
    public void カーソルを乗せたら立ち止まる()
    {
        var world = new World(Quiet());
        world.Put(120d);
        world.Run(3d);
        Assert.Equal(PetAction.Approach, world.Machine.CurrentAction);

        world.Hovering = true;
        var poses = world.Run(1d);

        Assert.All(poses, p => Assert.Equal(0d, p.TravelDots));
        Assert.NotEqual(PetAction.Approach, world.Machine.CurrentAction);
    }

    [Fact]
    public void たまごは近寄らない()
    {
        var world = new World(Quiet(), Awake() with { Growth = 0d });
        world.Put(40d);

        var poses = world.Run(10d);

        Assert.All(poses, p => Assert.Equal(0d, p.TravelDots));
    }

    [Fact]
    public void 向かっている途中で片付けられたら止まる()
    {
        var world = new World(Quiet());
        var item = world.Put(120d);
        world.Run(3d);
        Assert.Equal(PetAction.Approach, world.Machine.CurrentAction);

        world.Remove(item);
        var poses = world.Run(2d);

        Assert.All(poses, p => Assert.Equal(0d, p.TravelDots));
        Assert.Equal(PetAction.None, world.Machine.CurrentAction);
        Assert.Null(world.Machine.TakeArrival());
    }

    [Fact]
    public void 足場の端で届かないアイテムは諦めて粘らない()
    {
        var world = new World(Quiet()) { RoomAfter = 20d };
        world.Put(80d);

        world.Run(15d);
        Assert.Equal(PetAction.None, world.Machine.CurrentAction);

        // 諦めたあとは休む。端でその場に張りつかない
        var later = world.Run(30d);
        Assert.All(later, p => Assert.Equal(0d, p.TravelDots));
        Assert.Null(world.Machine.TakeArrival());
    }

    [Fact]
    public void しぐさを許していても気付いたらまずアイテムへ向かう()
    {
        var world = new World(new BehaviorMachine(3));
        world.Put(60d);

        var seen = new HashSet<PetAction>();
        for (var t = 0d; t < 2.5d; t += Frame)
        {
            world.Step();
            seen.Add(world.Machine.CurrentAction);
        }

        Assert.Contains(PetAction.Approach, seen);
    }

    [Fact]
    public void つついたあとはびっくりする()
    {
        var machine = Quiet();
        var state = Awake();

        Assert.True(machine.Trigger(PetAction.Poke));

        var seen = new List<PetAction>();
        for (var t = 0d; t < 2d; t += Frame)
        {
            machine.Advance(Frame, state);
            if (machine.CurrentAction != PetAction.None && !seen.Contains(machine.CurrentAction))
            {
                seen.Add(machine.CurrentAction);
            }
        }

        Assert.Equal(new[] { PetAction.Poke, PetAction.Startle }, seen);
        Assert.Equal(PetAction.None, machine.CurrentAction);
    }

    [Fact]
    public void きらきらを見たら目を丸くしてから喜ぶ()
    {
        var machine = Quiet();
        var state = Awake();
        Assert.True(machine.Trigger(PetAction.Admire));

        var poses = new List<PetPose>();
        for (var t = 0d; t < 1.5d; t += Frame)
        {
            poses.Add(machine.Advance(Frame, state));
        }

        Assert.Equal(EyePose.Surprised, poses[0].Eyes);
        Assert.Contains(poses, p => p.Eyes == EyePose.Happy && p.ShowHeart);
    }
}

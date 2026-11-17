using Mendako.Core.Model;

namespace Mendako.Core.Behavior;

/// <summary>
/// 状態と経過時間からコマを選ぶ。描画・Win32 に依存しないので単体でテストできる。
/// 乱数はシードを渡せるようにしてあり、まばたきの間隔やしぐさの順番まで再現可能。
/// </summary>
public sealed class BehaviorMachine
{
    private const double BlinkDuration = 0.18d;

    /// <summary>これより遠いカーソルは目で追わない。画面の反対側まで見ていると落ち着きがない。</summary>
    private const double GazeRangeDots = 45d;

    /// <summary>真上・真下にいるカーソルは正面を見る。</summary>
    private const double GazeDeadZoneDots = 3d;

    /// <summary>泳ぐ速さの上限（ドット / 秒）。</summary>
    private const double SwimSpeed = 5d;

    /// <summary>これより狭い側には泳ぎ出さない。</summary>
    private const double MinSwimRoomDots = 6d;

    /// <summary>
    /// 置いてあるアイテムに一度反応したら、同じアイテムへ近寄らない秒数。
    /// 片付けて置き直すと別のアイテムとして数えるので、その間も遊ばせたいときは置き直せばよい。
    /// </summary>
    internal const double ScatterCooldownSeconds = 600d;

    /// <summary>食べるしぐさ (<see cref="PetAction.Eat"/>) の長さ。食べられたアイテムは、これが終わるころに消す。</summary>
    public const double EatSeconds = 1.8d;

    /// <summary>これより遠いアイテムには気付かない。画面の端のものまで追いかけると落ち着きがない。</summary>
    private const double ScatterNoticeDots = 150d;

    /// <summary>気付いてから泳ぎ出すまでの間。そのあいだ目だけアイテムを追う。</summary>
    private const double ScatterNoticeDelaySeconds = 1d;

    /// <summary>
    /// アイテムの中心までこの距離に来たら着いたことにする。
    /// 体の幅の半分（10 ドット）より少し外で止まるので、アイテムを踏まずに横から覗き込む形になる。
    /// </summary>
    private const double ScatterArriveDots = 11d;

    /// <summary>アイテムへ向かうときの速さの上限（ドット / 秒）。ぶらぶら泳ぐ <see cref="SwimSpeed"/> よりも急ぐ。</summary>
    private const double ApproachSpeed = 10d;

    /// <summary>着けなかったときに諦めるまでの秒数。</summary>
    private const double ApproachTimeoutSeconds = 45d;

    /// <summary>つつく動きの長さ。これが終わるとびっくりに替わる。</summary>
    private const double PokeSeconds = 0.8d;

    private readonly Random _random;
    private readonly Dictionary<Guid, double> _scatterCooldownUntil = new();

    private double _time;
    private double _nextBlinkAt;
    private double _blinkStartedAt = double.NegativeInfinity;
    private double _nextGestureAt;
    private double _actionStartedAt;
    private double _actionEndsAt;

    /// <summary>泳ぐ向き。0 は未定で、泳ぎ出しのフレームで余地を見て決める。</summary>
    private int _swimDirection;

    private Guid _approachTarget;
    private Guid _noticedTarget;
    private double _noticedSince;
    private int _noticeGaze;

    /// <summary>最後に近寄ったときの向き。つつくときにそちらへ体を出す。</summary>
    private int _facing = 1;

    private Guid? _arrival;

    public BehaviorMachine(int? seed = null)
    {
        _random = seed is { } s ? new Random(s) : new Random();
        _nextBlinkAt = NextBlinkInterval();
        _nextGestureAt = NextGestureInterval();
    }

    public PetAction CurrentAction { get; private set; } = PetAction.None;

    /// <summary>ひとりでにしぐさを始めるか。false でも <see cref="Trigger"/> で起こすことはできる。</summary>
    public bool GesturesEnabled { get; init; } = true;

    /// <summary>
    /// 一時的な動きを開始する。実行中のものより優先度が低ければ無視して false を返す。
    /// 同じ優先度なら後から来たほうが勝つ (連打したら最後の操作に反応してほしい)。
    /// </summary>
    public bool Trigger(PetAction action, double? durationSeconds = null)
    {
        if (action == PetAction.None)
        {
            return false;
        }

        // 終了時刻を過ぎていても、次の Advance までは CurrentAction が残っている
        var running = _time < _actionEndsAt ? CurrentAction : PetAction.None;
        if (Priority(action) < Priority(running))
        {
            return false;
        }

        CurrentAction = action;
        _actionStartedAt = _time;
        _actionEndsAt = _time + (durationSeconds ?? DefaultDuration(action));
        _swimDirection = 0;
        return true;
    }

    /// <summary>
    /// 置いてあるアイテムのそばに着いたら、そのアイテムの識別子を一度だけ返す。
    /// 反応（食べる・喜ぶ）は育成状態に関わるので、どう反応するかは呼び出し側が決めて <see cref="Trigger"/> する。
    /// </summary>
    public Guid? TakeArrival()
    {
        var arrival = _arrival;
        _arrival = null;
        return arrival;
    }

    /// <summary>
    /// 実行中の動きを切り上げる。別の動きに替わっていれば何もしない。
    /// つかまれている・落ちている、のように終わりを外から決める動きに使う。
    /// </summary>
    public void Stop(PetAction action)
    {
        if (CurrentAction == action)
        {
            CurrentAction = PetAction.None;
            _actionEndsAt = _time;
        }
    }

    /// <summary>指定秒数だけ時間を進め、そのフレームのコマを返す。</summary>
    public PetPose Advance(double deltaSeconds, MendakoState state, BehaviorInput input = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var delta = Math.Max(0d, deltaSeconds);
        _time += delta;

        if (CurrentAction != PetAction.None && _time >= _actionEndsAt)
        {
            var finished = CurrentAction;
            CurrentAction = PetAction.None;

            if (finished == PetAction.Approach)
            {
                // 着けないまま時間切れ。休ませないと、同じ Advance のうちに見る間も無く近寄り直す
                EndApproachWithCooldown(_approachTarget);
            }
            else if (finished == PetAction.Poke && !state.IsAsleep)
            {
                Trigger(PetAction.Startle);
            }
        }

        // 見ている途中で別の動きに割り込まれたら、見る間は最初からやり直す。飛ばすと終わった途端に泳ぎ出す
        if (CurrentAction is not (PetAction.None or PetAction.Approach))
        {
            _noticedTarget = Guid.Empty;
        }

        // カーソルを乗せたら立ち止まる。泳いで逃げられるとクリックできない
        if (CurrentAction is PetAction.Swim or PetAction.Approach && input.Hovering)
        {
            CurrentAction = PetAction.None;
        }

        // 眠ったら近寄るのをやめる。起きたあとはまた気付けば近寄る
        if (CurrentAction == PetAction.Approach && state.IsAsleep)
        {
            CurrentAction = PetAction.None;
        }

        if (state.IsAsleep && CurrentAction == PetAction.None)
        {
            return SleepingPose();
        }

        if (CurrentAction == PetAction.None && !TryNoticeScattered(state, input))
        {
            StartGestureIfDue(state, input);
        }

        var pose = CurrentAction switch
        {
            PetAction.Eat => EatingPose(),
            PetAction.Happy => HappyPose(),
            PetAction.Refuse => RefusingPose(),
            PetAction.Evolve => EvolvingPose(),
            PetAction.Startle => StartledPose(),
            PetAction.Admire => AdmiringPose(),
            PetAction.Poke => PokingPose(),
            PetAction.Held => HeldPose(),
            PetAction.Fall => FallingPose(),
            PetAction.Land => LandingPose(),
            PetAction.LookAround => LookingAroundPose(state.Mood),
            PetAction.Flutter => FlutteringPose(),
            PetAction.Yawn => YawningPose(),
            PetAction.Flatten => FlattenedPose(),
            PetAction.Wobble => WobblingPose(state.Mood),
            PetAction.Swim => SwimmingPose(delta, input),
            PetAction.Approach => ApproachingPose(delta, state, input),
            _ => IdlePose(state.Mood) with { GazeDots = _noticeGaze != 0 ? _noticeGaze : GazeToward(input) },
        };

        // 目を開けているコマにだけまばたきを載せる。閉じ目やにっこりは動き側が決めたもの
        return pose.Eyes == EyePose.Open
            ? pose with { Eyes = ResolveEyes() }
            : pose;
    }

    // --- 待機 ---

    private PetPose IdlePose(Mood mood)
    {
        var (bobDots, speed, droops) = IdleParameters(mood);

        // パタパタは Up / Mid の 2 コマ。矩形波にすることでドット絵らしい動きになる
        var flap = Math.Sin(_time * speed * 1.6d);

        return new PetPose
        {
            Fin = droops ? FinPose.Droop : flap > 0d ? FinPose.Up : FinPose.Mid,
            Eyes = EyePose.Open,
            BobDots = Math.Sin(_time * speed * 1.05d) * bobDots,
            DriftDots = Math.Sin(_time * speed * 0.31d) * 0.6d,
        };
    }

    private static (double BobDots, double Speed, bool Droops) IdleParameters(Mood mood) => mood switch
    {
        Mood.Happy => (1.6d, 1.35d, false),
        Mood.Content => (1.2d, 1.0d, false),
        Mood.Hungry => (0.9d, 0.8d, false),
        Mood.Sleepy => (0.7d, 0.6d, true),
        Mood.Lonely => (0.9d, 0.7d, false),
        Mood.Gloomy => (0.6d, 0.5d, true),
        _ => (1.2d, 1.0d, false),
    };

    /// <summary>近くにあるカーソルのほうへ目を向ける。</summary>
    private static int GazeToward(BehaviorInput input)
    {
        if (input.CursorDots is not { } cursor)
        {
            return 0;
        }

        if (Math.Abs(cursor.X) < GazeDeadZoneDots
            || (cursor.X * cursor.X) + (cursor.Y * cursor.Y) > GazeRangeDots * GazeRangeDots)
        {
            return 0;
        }

        return Math.Sign(cursor.X);
    }

    // --- 睡眠 ---

    private PetPose SleepingPose() => new()
    {
        Fin = FinPose.Droop,
        Eyes = EyePose.Closed,
        BobDots = Math.Sin(_time * 0.45d) * 0.8d,
        ShowSleepMark = true,
    };

    // --- リアクション ---

    private double Elapsed => _time - _actionStartedAt;

    private PetPose EatingPose()
    {
        var chew = Math.Sin(Elapsed * 14d);

        return new PetPose
        {
            Fin = chew > 0d ? FinPose.Up : FinPose.Mid,
            Eyes = EyePose.Closed,
            BobDots = chew > 0d ? -1d : 0d,
        };
    }

    private PetPose HappyPose() => new()
    {
        Fin = Math.Sin(Elapsed * 12d) > 0d ? FinPose.Up : FinPose.Mid,
        Eyes = EyePose.Happy,
        BobDots = -Math.Abs(Math.Sin(Elapsed * 5d)) * 2d,
        ShowHeart = true,
    };

    private PetPose RefusingPose() => new()
    {
        Fin = FinPose.Droop,
        Eyes = EyePose.Closed,
        DriftDots = Math.Sin(Elapsed * 16d) * 1.2d,
    };

    private PetPose EvolvingPose() => new()
    {
        Fin = Math.Sin(Elapsed * 9d) > 0d ? FinPose.Up : FinPose.Mid,
        Eyes = EyePose.Happy,
        BobDots = -Math.Abs(Math.Sin(Elapsed * 3d)) * 3d,
        ShowHeart = true,
    };

    private PetPose StartledPose() => new()
    {
        Fin = FinPose.Up,
        Eyes = EyePose.Surprised,

        // ぴょんと跳ねて、すぐ降りる
        BobDots = -Math.Sin(Math.Min(1d, Elapsed / 0.3d) * Math.PI) * 3d,
    };

    private PetPose AdmiringPose()
    {
        const double StareSeconds = 0.7d;
        var staring = Elapsed < StareSeconds;

        return new PetPose
        {
            Fin = Math.Sin(Elapsed * 7d) > 0d ? FinPose.Up : FinPose.Mid,
            Eyes = staring ? EyePose.Surprised : EyePose.Happy,
            BobDots = -Math.Abs(Math.Sin(Elapsed * 3.5d)) * 1.5d,
            ShowHeart = !staring,
        };
    }

    private PetPose PokingPose() => new()
    {
        Fin = FinPose.Up,
        Eyes = EyePose.Open,
        GazeDots = _facing,
        DriftDots = _facing * Math.Abs(Math.Sin(Elapsed / PokeSeconds * Math.PI * 2d)) * 2d,
    };

    // --- つまみ上げ ---

    private PetPose HeldPose() => new()
    {
        // じたばた
        Fin = Math.Sin(Elapsed * 18d) > 0d ? FinPose.Up : FinPose.Mid,
        Eyes = EyePose.Surprised,
        DriftDots = Math.Sin(Elapsed * 9d) * 0.8d,
    };

    private PetPose FallingPose() => new()
    {
        // 傘を広げて、ゆらゆら揺れながら降りる
        Fin = FinPose.Up,
        Eyes = EyePose.Open,
        DriftDots = Math.Sin(Elapsed * 2.2d) * 2d,
    };

    private static PetPose LandingPose() => new()
    {
        Fin = FinPose.Droop,
        Eyes = EyePose.Closed,
        Flat = true,
    };

    // --- しぐさ ---

    private void StartGestureIfDue(MendakoState state, BehaviorInput input)
    {
        if (!GesturesEnabled || _time < _nextGestureAt)
        {
            return;
        }

        _nextGestureAt = _time + NextGestureInterval();

        var gesture = PickGesture(state, input);
        if (gesture != PetAction.None)
        {
            Trigger(gesture, gesture == PetAction.Swim ? 3d + (_random.NextDouble() * 4d) : null);
        }
    }

    /// <summary>気分に合ったしぐさを選ぶ。ごきげんならよく動き、しょんぼりならぺたんとしがち。</summary>
    private PetAction PickGesture(MendakoState state, BehaviorInput input)
    {
        if (state.Stage == GrowthStage.Egg)
        {
            return PetAction.Wobble;
        }

        var (look, flutter, yawn, flatten, swim) = state.Mood switch
        {
            Mood.Happy => (2d, 4d, 0d, 1d, 4d),
            Mood.Hungry => (4d, 0d, 0d, 2d, 3d),
            Mood.Sleepy => (1d, 0d, 5d, 3d, 0d),
            Mood.Lonely => (5d, 0d, 0d, 2d, 2d),
            Mood.Gloomy => (1d, 0d, 1d, 5d, 0d),
            _ => (3d, 2d, 1d, 2d, 3d),
        };

        if (input.Hovering || !CanSwim(input))
        {
            swim = 0d;
        }

        var roll = _random.NextDouble() * (look + flutter + yawn + flatten + swim);

        if ((roll -= look) < 0d)
        {
            return PetAction.LookAround;
        }

        if ((roll -= flutter) < 0d)
        {
            return PetAction.Flutter;
        }

        if ((roll -= yawn) < 0d)
        {
            return PetAction.Yawn;
        }

        return (roll - flatten) < 0d ? PetAction.Flatten : PetAction.Swim;
    }

    private static bool CanSwim(BehaviorInput input) =>
        input.RoomBeforeDots >= MinSwimRoomDots || input.RoomAfterDots >= MinSwimRoomDots;

    private PetPose LookingAroundPose(Mood mood)
    {
        // 左を見て、ひと呼吸おいて右を見て、正面に戻る
        var gaze = Elapsed switch
        {
            < 0.6d => -1,
            < 0.75d => 0,
            < 1.35d => 1,
            _ => 0,
        };

        return IdlePose(mood) with { GazeDots = gaze, DriftDots = 0d };
    }

    private PetPose FlutteringPose() => new()
    {
        Fin = Math.Sin(Elapsed * 26d) > 0d ? FinPose.Up : FinPose.Mid,
        Eyes = EyePose.Open,
        BobDots = -Math.Abs(Math.Sin(Elapsed * 8d)) * 1.5d,
    };

    private PetPose YawningPose()
    {
        const double StretchSeconds = 1.4d;

        // 耳ビレを持ち上げて伸び上がり、ふっと力を抜く
        var stretching = Elapsed < StretchSeconds;

        return new PetPose
        {
            Fin = stretching ? FinPose.Up : FinPose.Droop,
            Eyes = EyePose.Closed,
            BobDots = stretching ? -Math.Sin(Elapsed / StretchSeconds * Math.PI) * 2d : 0d,
        };
    }

    private static PetPose FlattenedPose() => new()
    {
        Fin = FinPose.Droop,
        Eyes = EyePose.Open,
        Flat = true,
    };

    private PetPose WobblingPose(Mood mood) =>
        IdlePose(mood) with { DriftDots = Math.Sin(Elapsed * 18d) };

    private PetPose SwimmingPose(double delta, BehaviorInput input)
    {
        if (_swimDirection == 0)
        {
            _swimDirection = PickSwimDirection(input);
        }

        var room = _swimDirection > 0 ? input.RoomAfterDots : input.RoomBeforeDots;

        // 傘をすぼめた瞬間にぐっと進み、開くあいだは惰性で流れる
        var stroke = Math.Abs(Math.Sin(Elapsed * 3d));
        var travel = Math.Min(Math.Max(0d, room), SwimSpeed * (0.35d + (0.65d * stroke)) * delta);

        if (room <= 0d)
        {
            // 端まで来た。次のフレームで待機に戻る
            _actionEndsAt = _time;
        }

        return new PetPose
        {
            Fin = Math.Sin(Elapsed * 10d) > 0d ? FinPose.Up : FinPose.Mid,
            Eyes = EyePose.Open,
            GazeDots = input.VerticalRail ? 0 : _swimDirection,
            BobDots = -stroke * 2d,
            TravelDots = travel * _swimDirection,
        };
    }

    /// <summary>広いほうへ行きやすくする。端に寄っているときに端へ向かっても、すぐ止まるだけなので。</summary>
    private int PickSwimDirection(BehaviorInput input)
    {
        var before = input.RoomBeforeDots >= MinSwimRoomDots ? input.RoomBeforeDots : 0d;
        var after = input.RoomAfterDots >= MinSwimRoomDots ? input.RoomAfterDots : 0d;

        if (before + after <= 0d)
        {
            return 1;
        }

        return _random.NextDouble() * (before + after) < after ? 1 : -1;
    }

    // --- 置いてあるアイテム ---

    /// <summary>
    /// 近くにアイテムがあれば気付いて、少し見てから泳ぎ出す。気付いている間は true を返し、
    /// 待機やしぐさに戻らせない (見ている最中に伸びをすると、気付いていないように見える)。
    /// </summary>
    private bool TryNoticeScattered(MendakoState state, BehaviorInput input)
    {
        _noticeGaze = 0;

        if (state.IsAsleep || state.Stage == GrowthStage.Egg || input.Hovering)
        {
            _noticedTarget = Guid.Empty;
            return false;
        }

        if (PickScatterTarget(input) is not { } target)
        {
            _noticedTarget = Guid.Empty;
            return false;
        }

        if (target.Id != _noticedTarget)
        {
            _noticedTarget = target.Id;
            _noticedSince = _time;
        }

        if (!input.VerticalRail)
        {
            _noticeGaze = Math.Sign(target.OffsetDots);
        }

        if (_time - _noticedSince >= ScatterNoticeDelaySeconds)
        {
            _approachTarget = target.Id;
            Trigger(PetAction.Approach);
        }

        return true;
    }

    /// <summary>気付く範囲にある、クールダウン中でない一番近いアイテム。</summary>
    private ScatterTarget? PickScatterTarget(BehaviorInput input)
    {
        if (input.Scattered is not { Count: > 0 } scattered)
        {
            return null;
        }

        ScatterTarget? nearest = null;
        foreach (var candidate in scattered)
        {
            var distance = Math.Abs(candidate.OffsetDots);
            if (distance > ScatterNoticeDots
                || (_scatterCooldownUntil.TryGetValue(candidate.Id, out var until) && _time < until))
            {
                continue;
            }

            if (nearest is null || distance < Math.Abs(nearest.Value.OffsetDots))
            {
                nearest = candidate;
            }
        }

        return nearest;
    }

    private PetPose ApproachingPose(double delta, MendakoState state, BehaviorInput input)
    {
        var found = input.Scattered?
            .Where(candidate => candidate.Id == _approachTarget)
            .Select(candidate => (ScatterTarget?)candidate)
            .FirstOrDefault();

        var idle = IdlePose(state.Mood);

        if (found is not { } target)
        {
            EndApproach();
            return idle;
        }

        var direction = target.OffsetDots < 0d ? -1 : 1;
        var distance = Math.Abs(target.OffsetDots);

        if (distance <= ScatterArriveDots)
        {
            // 着いたら反応を渡して立ち止まる。同じアイテムへすぐ戻ってこないよう、反応の結果によらず休ませる
            _arrival = target.Id;
            _facing = direction;
            EndApproachWithCooldown(target.Id);
            return idle with { GazeDots = input.VerticalRail ? 0 : direction };
        }

        var room = direction > 0 ? input.RoomAfterDots : input.RoomBeforeDots;
        if (room <= 0d)
        {
            // 足場の端で体が届かない場所にある。粘ってもその場で止まるだけなので、休ませて諦める
            EndApproachWithCooldown(target.Id);
            return idle;
        }

        _facing = direction;

        var stroke = Math.Abs(Math.Sin(Elapsed * 3d));

        // 着く距離のわずかに手前までしか進まない。行き過ぎて戻ると、アイテムの前で行ったり来たりする
        var travel = Math.Min(
            Math.Min(room, distance - ScatterArriveDots + 0.5d),
            ApproachSpeed * (0.35d + (0.65d * stroke)) * delta);

        return new PetPose
        {
            Fin = Math.Sin(Elapsed * 10d) > 0d ? FinPose.Up : FinPose.Mid,
            Eyes = EyePose.Open,
            GazeDots = input.VerticalRail ? 0 : direction,
            BobDots = -stroke * 2d,
            TravelDots = travel * direction,
        };
    }

    private void EndApproach()
    {
        CurrentAction = PetAction.None;
        _actionEndsAt = _time;
        _noticedTarget = Guid.Empty;
    }

    /// <summary>近寄りをやめて、同じアイテムへすぐ戻らないよう休ませる。着いた・届かない・時間切れのどれでも同じ。</summary>
    private void EndApproachWithCooldown(Guid id)
    {
        _scatterCooldownUntil[id] = _time + ScatterCooldownSeconds;
        EndApproach();
    }

    private double NextGestureInterval() => 8d + (_random.NextDouble() * 14d);

    // --- まばたき ---

    private EyePose ResolveEyes()
    {
        if (_time >= _nextBlinkAt && double.IsNegativeInfinity(_blinkStartedAt))
        {
            _blinkStartedAt = _time;
        }

        if (double.IsNegativeInfinity(_blinkStartedAt))
        {
            return EyePose.Open;
        }

        if (_time - _blinkStartedAt >= BlinkDuration)
        {
            _blinkStartedAt = double.NegativeInfinity;
            _nextBlinkAt = _time + NextBlinkInterval();
            return EyePose.Open;
        }

        return EyePose.Closed;
    }

    private double NextBlinkInterval() => 2.5d + (_random.NextDouble() * 4.5d);

    /// <summary>
    /// しぐさは世話のリアクションに譲る。段階アップは一度きりなので、さらに一段上に置く。
    /// </summary>
    private static int Priority(PetAction action) => action switch
    {
        PetAction.None => 0,
        PetAction.Eat or PetAction.Happy or PetAction.Refuse or PetAction.Startle or PetAction.Admire or PetAction.Poke => 2,
        PetAction.Held or PetAction.Fall or PetAction.Land => 2,
        PetAction.Evolve => 3,
        _ => 1,
    };

    private static double DefaultDuration(PetAction action) => action switch
    {
        PetAction.Eat => EatSeconds,
        PetAction.Happy => 1.6d,
        PetAction.Refuse => 0.9d,
        PetAction.Evolve => 2.6d,
        PetAction.Startle => 0.6d,
        PetAction.Admire => 1.8d,
        PetAction.Poke => PokeSeconds,
        PetAction.Held => double.PositiveInfinity,
        PetAction.Fall => double.PositiveInfinity,
        PetAction.Land => 0.35d,
        PetAction.LookAround => 1.8d,
        PetAction.Flutter => 1.2d,
        PetAction.Yawn => 2.2d,
        PetAction.Flatten => 3.0d,
        PetAction.Wobble => 0.8d,
        PetAction.Swim => 5.0d,
        PetAction.Approach => ApproachTimeoutSeconds,
        _ => 0d,
    };
}

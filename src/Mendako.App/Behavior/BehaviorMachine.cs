using System;
using Mendako.App.Sprites;
using Mendako.Core.Model;

namespace Mendako.App.Behavior;

/// <summary>
/// 状態と経過時間からコマを選ぶ。描画・Win32 に依存しないので単体でテストできる。
/// 乱数はシードを渡せるようにしてあり、まばたきの間隔まで再現可能。
/// </summary>
public sealed class BehaviorMachine
{
    private const double BlinkDuration = 0.18d;

    private readonly Random _random;

    private double _time;
    private double _nextBlinkAt;
    private double _blinkStartedAt = double.NegativeInfinity;
    private double _actionStartedAt;
    private double _actionEndsAt;

    public BehaviorMachine(int? seed = null)
    {
        _random = seed is { } s ? new Random(s) : new Random();
        _nextBlinkAt = NextBlinkInterval();
    }

    public PetAction CurrentAction { get; private set; } = PetAction.None;

    /// <summary>一時的なリアクションを開始する。実行中のものは上書きされる。</summary>
    public void Trigger(PetAction action, double? durationSeconds = null)
    {
        CurrentAction = action;
        _actionStartedAt = _time;
        _actionEndsAt = _time + (durationSeconds ?? DefaultDuration(action));
    }

    /// <summary>指定秒数だけ時間を進め、そのフレームのコマを返す。</summary>
    public PetPose Advance(double deltaSeconds, MendakoState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _time += Math.Max(0d, deltaSeconds);

        if (CurrentAction != PetAction.None && _time >= _actionEndsAt)
        {
            CurrentAction = PetAction.None;
        }

        if (state.IsAsleep && CurrentAction == PetAction.None)
        {
            return SleepingPose();
        }

        var pose = CurrentAction switch
        {
            PetAction.Eat => EatingPose(),
            PetAction.Happy => HappyPose(),
            PetAction.Refuse => RefusingPose(),
            PetAction.Evolve => EvolvingPose(),
            _ => IdlePose(state.Mood),
        };

        // リアクション側で目のコマを決めているものはそのまま、それ以外はまばたきを載せる
        return CurrentAction is PetAction.Happy or PetAction.Evolve
            ? pose
            : pose with { Eyes = ResolveEyes() };
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

    // --- 睡眠 ---

    private PetPose SleepingPose() => new()
    {
        Fin = FinPose.Droop,
        Eyes = EyePose.Closed,
        BobDots = Math.Sin(_time * 0.45d) * 0.8d,
        ShowSleepMark = true,
    };

    // --- リアクション ---

    private PetPose EatingPose()
    {
        var elapsed = _time - _actionStartedAt;
        var chew = Math.Sin(elapsed * 14d);

        return new PetPose
        {
            Fin = chew > 0d ? FinPose.Up : FinPose.Mid,
            Eyes = EyePose.Closed,
            BobDots = chew > 0d ? -1d : 0d,
        };
    }

    private PetPose HappyPose()
    {
        var elapsed = _time - _actionStartedAt;

        return new PetPose
        {
            Fin = Math.Sin(elapsed * 12d) > 0d ? FinPose.Up : FinPose.Mid,
            Eyes = EyePose.Happy,
            BobDots = -Math.Abs(Math.Sin(elapsed * 5d)) * 2d,
            ShowHeart = true,
        };
    }

    private PetPose RefusingPose()
    {
        var elapsed = _time - _actionStartedAt;

        return new PetPose
        {
            Fin = FinPose.Droop,
            Eyes = EyePose.Closed,
            DriftDots = Math.Sin(elapsed * 16d) * 1.2d,
        };
    }

    private PetPose EvolvingPose()
    {
        var elapsed = _time - _actionStartedAt;

        return new PetPose
        {
            Fin = Math.Sin(elapsed * 9d) > 0d ? FinPose.Up : FinPose.Mid,
            Eyes = EyePose.Happy,
            BobDots = -Math.Abs(Math.Sin(elapsed * 3d)) * 3d,
            ShowHeart = true,
        };
    }

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

    private static double DefaultDuration(PetAction action) => action switch
    {
        PetAction.Eat => 1.8d,
        PetAction.Happy => 1.6d,
        PetAction.Refuse => 0.9d,
        PetAction.Evolve => 2.6d,
        _ => 0d,
    };
}

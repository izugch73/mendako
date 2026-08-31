using Mendako.Core.Model;

namespace Mendako.Core.Simulation;

/// <summary>
/// 育成シミュレーションの本体。すべて純粋関数で、UI にも時計にも依存しない。
/// 「3 日放置したらどうなるか」をテストで即座に検証できるのが狙い。
/// </summary>
public static class Simulator
{
    /// <summary>
    /// <paramref name="state"/> を <paramref name="nowUtc"/> まで進める。
    /// アプリが動いていなかった時間もここでまとめて消化される (オフライン進行)。
    /// </summary>
    /// <param name="timeZone">昼夜の判定に使うタイムゾーン。テストでは <see cref="TimeZoneInfo.Utc"/> を渡す。</param>
    public static AdvanceOutcome Advance(
        MendakoState state,
        DateTimeOffset nowUtc,
        TimeZoneInfo timeZone,
        SimulationConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(timeZone);
        config ??= SimulationConfig.Default;

        var elapsed = nowUtc - state.LastTickUtc;

        // 時計が巻き戻された (手動変更・タイムゾーン変更・NTP 補正) 場合は
        // 進めずに基準時刻だけ現在に合わせ直す。巻き戻しで得をさせない。
        if (elapsed < TimeSpan.Zero)
        {
            return new AdvanceOutcome(state with { LastTickUtc = nowUtc }, 0, 0);
        }

        var tickMinutes = config.TickInterval.TotalMinutes;
        var totalTicks = (long)Math.Floor(elapsed.TotalMinutes / tickMinutes);
        if (totalTicks <= 0)
        {
            return new AdvanceOutcome(state, 0, 0);
        }

        var applied = Math.Min(totalTicks, config.MaxCatchUpTicks);
        var skipped = totalTicks - applied;

        // 打ち切る場合は「直近の applied ティック分」を採用する。
        // 古い方を捨てることで、帰ってきた直後の状態が現在に即したものになる。
        var simStart = state.LastTickUtc.AddMinutes(skipped * tickMinutes);

        var stageBefore = state.Stage;
        var asleepBefore = state.IsAsleep;

        var satiety = state.Satiety;
        var energy = state.Energy;
        var affection = state.Affection;
        var growth = state.Growth;
        var asleep = state.IsAsleep;
        var fellAsleep = false;
        var wokeUp = false;

        for (long i = 0; i < applied; i++)
        {
            var tickAt = simStart.AddMinutes(i * tickMinutes);
            var localHour = TimeZoneInfo.ConvertTime(tickAt, timeZone).Hour;

            var nextAsleep = DecideAsleep(asleep, energy, localHour, config);
            if (nextAsleep && !asleep)
            {
                fellAsleep = true;
            }
            else if (!nextAsleep && asleep)
            {
                wokeUp = true;
            }

            asleep = nextAsleep;

            var satietyDecay = config.SatietyDecayPerTick
                * (asleep ? config.SleepSatietyDecayMultiplier : 1d);
            satiety = Math.Clamp(satiety - satietyDecay, 0d, 100d);

            energy = asleep
                ? Math.Clamp(energy + config.EnergyRecoveryPerTick, 0d, 100d)
                : Math.Clamp(energy - config.EnergyDecayPerTick, 0d, 100d);

            affection = Math.Clamp(affection - config.AffectionDecayPerTick, 0d, 100d);

            growth += GrowthPerTick(satiety, energy, affection, asleep, config);
        }

        var next = state with
        {
            Satiety = satiety,
            Energy = energy,
            Affection = affection,
            Growth = growth,
            IsAsleep = asleep,
            LivedMinutes = state.LivedMinutes + (long)(applied * tickMinutes),

            // 打ち切った分も含めて基準時刻は現在まで進める。
            // 端数 (1 ティック未満) は次回に持ち越されるので時間が失われない。
            LastTickUtc = state.LastTickUtc.AddMinutes(totalTicks * tickMinutes),
            LastSkippedMinutes = (long)(skipped * tickMinutes),
        };

        return new AdvanceOutcome(next, applied, skipped)
        {
            StageAdvancedTo = next.Stage > stageBefore ? next.Stage : null,
            FellAsleep = fellAsleep && !asleepBefore,
            WokeUp = wokeUp && asleepBefore,
        };
    }

    /// <summary>エサをあげる。</summary>
    public static CareOutcome Feed(MendakoState state, DateTimeOffset nowUtc, SimulationConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        config ??= SimulationConfig.Default;

        if (state.IsAsleep)
        {
            return new CareOutcome(state, CareResult.RefusedAsleep);
        }

        if (state.Satiety >= config.FeedRefuseThreshold)
        {
            return new CareOutcome(state, CareResult.RefusedTooFull);
        }

        var next = state with
        {
            Satiety = Math.Clamp(state.Satiety + config.FeedSatietyGain, 0d, 100d),
            Affection = Math.Clamp(state.Affection + config.FeedAffectionGain, 0d, 100d),
            LastFedUtc = nowUtc,
            FeedCount = state.FeedCount + 1,
        };

        return new CareOutcome(next, CareResult.Accepted);
    }

    /// <summary>なでる。クールダウン中でも反応はするが、なつき度は上がらない。</summary>
    public static CareOutcome Pet(MendakoState state, DateTimeOffset nowUtc, SimulationConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        config ??= SimulationConfig.Default;

        if (state.IsAsleep)
        {
            return new CareOutcome(state, CareResult.RefusedAsleep);
        }

        var onCooldown = state.LastPettedUtc is { } last
            && nowUtc - last < config.PetCooldown;

        if (onCooldown)
        {
            return new CareOutcome(state with { PetCount = state.PetCount + 1 }, CareResult.AcceptedWithoutGain);
        }

        var next = state with
        {
            Affection = Math.Clamp(state.Affection + config.PetAffectionGain, 0d, 100d),
            LastPettedUtc = nowUtc,
            PetCount = state.PetCount + 1,
        };

        return new CareOutcome(next, CareResult.Accepted);
    }

    /// <summary>手動で寝かせる / 起こす。</summary>
    public static MendakoState SetAsleep(MendakoState state, bool asleep)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.IsAsleep == asleep ? state : state with { IsAsleep = asleep };
    }

    public static MendakoState Rename(MendakoState state, string name)
    {
        ArgumentNullException.ThrowIfNull(state);
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) ? state : state with { Name = trimmed };
    }

    /// <summary>1 ティックあたりの成長ポイント。世話が行き届いているほど大きくなる。</summary>
    internal static double GrowthPerTick(
        double satiety,
        double energy,
        double affection,
        bool asleep,
        SimulationConfig config)
    {
        if (satiety <= config.GrowthSatietyFloor)
        {
            return 0d;
        }

        var quality = (0.4d * (satiety / 100d))
            + (0.3d * (energy / 100d))
            + (0.3d * (affection / 100d));

        var multiplier = asleep ? config.SleepGrowthMultiplier : 1d;
        return config.MaxGrowthPerTick * quality * multiplier;
    }

    /// <summary>次のティックで眠っているべきかを判定する。</summary>
    internal static bool DecideAsleep(bool asleep, double energy, int localHour, SimulationConfig config)
    {
        var night = IsNight(localHour, config);

        if (asleep)
        {
            // 十分に寝たら、夜でも目を覚ます。
            if (energy >= 99.5d)
            {
                return false;
            }

            return night || energy < config.WakeEnergy;
        }

        if (energy <= config.ForcedSleepEnergy)
        {
            return true;
        }

        return night && energy < config.NightSleepEnergy;
    }

    /// <summary>夜の時間帯か。日をまたぐ設定 (23 時 - 7 時) に対応する。</summary>
    internal static bool IsNight(int hour, SimulationConfig config)
    {
        if (config.SleepStartHour <= config.SleepEndHour)
        {
            return hour >= config.SleepStartHour && hour < config.SleepEndHour;
        }

        return hour >= config.SleepStartHour || hour < config.SleepEndHour;
    }
}

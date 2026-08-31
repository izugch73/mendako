using System;
using System.Windows.Threading;
using Mendako.Core;
using Mendako.Core.Model;
using Mendako.Core.Simulation;

namespace Mendako.App.Services;

/// <summary>
/// 育成状態の持ち主。シミュレーションの駆動と永続化をまとめる。
/// UI からはここだけを触ればよい。
/// </summary>
public sealed class MendakoSession : IDisposable
{
    /// <summary>シミュレーションを進める間隔。ティック (1 分) より短くしておけば取りこぼさない。</summary>
    private static readonly TimeSpan AdvanceInterval = TimeSpan.FromSeconds(20);

    /// <summary>自動保存の間隔。毎ティック保存すると SSD に優しくない。</summary>
    private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromMinutes(3);

    private readonly IClock _clock;
    private readonly SimulationConfig _config;
    private readonly JsonFileStore<MendakoState> _store;
    private readonly DispatcherTimer _advanceTimer;
    private readonly DispatcherTimer _autoSaveTimer;

    private bool _dirty;
    private bool _disposed;

    public MendakoSession(IClock? clock = null, SimulationConfig? config = null, JsonFileStore<MendakoState>? store = null)
    {
        _clock = clock ?? SystemClock.Instance;
        _config = config ?? SimulationConfig.Default;
        _store = store ?? new JsonFileStore<MendakoState>(AppPaths.StateFile);

        State = _store.Load() ?? MendakoState.CreateNew(_clock.UtcNow);

        _advanceTimer = new DispatcherTimer { Interval = AdvanceInterval };
        _advanceTimer.Tick += (_, _) => Advance();

        _autoSaveTimer = new DispatcherTimer { Interval = AutoSaveInterval };
        _autoSaveTimer.Tick += (_, _) => SaveIfDirty();
    }

    public MendakoState State { get; private set; }

    /// <summary>状態が変わるたびに発火する。</summary>
    public event EventHandler<MendakoState>? StateChanged;

    /// <summary>成長段階が上がったときに発火する。</summary>
    public event EventHandler<GrowthStage>? StageAdvanced;

    /// <summary>長期間の不在から復帰したときに、打ち切られた分数とともに発火する。</summary>
    public event EventHandler<long>? ReturnedAfterAbsence;

    /// <summary>起動直後のキャッチアップを行い、タイマーを回し始める。</summary>
    public void Start()
    {
        Advance();
        _advanceTimer.Start();
        _autoSaveTimer.Start();
    }

    /// <summary>現在時刻までシミュレーションを進める。</summary>
    public void Advance()
    {
        var outcome = Simulator.Advance(State, _clock.UtcNow, _clock.LocalTimeZone, _config);
        if (ReferenceEquals(outcome.State, State))
        {
            return;
        }

        State = outcome.State;
        _dirty = true;
        StateChanged?.Invoke(this, State);

        if (outcome.TicksSkipped > 0)
        {
            ReturnedAfterAbsence?.Invoke(this, outcome.TicksSkipped);
        }

        if (outcome.StageAdvancedTo is { } stage)
        {
            StageAdvanced?.Invoke(this, stage);
        }
    }

    public CareResult Feed()
    {
        Advance();

        var outcome = Simulator.Feed(State, _clock.UtcNow, _config);
        Apply(outcome);
        return outcome.Result;
    }

    public CareResult Pet()
    {
        Advance();

        var outcome = Simulator.Pet(State, _clock.UtcNow, _config);
        Apply(outcome);
        return outcome.Result;
    }

    public void ToggleSleep()
    {
        Advance();
        UpdateState(Simulator.SetAsleep(State, !State.IsAsleep));
    }

    public void Rename(string name) => UpdateState(Simulator.Rename(State, name));

    /// <summary>飼い直す。現在の状態は失われる。</summary>
    public void Reset(string? name = null) =>
        UpdateState(MendakoState.CreateNew(_clock.UtcNow, name ?? State.Name));

    public void SaveNow()
    {
        _store.Save(State);
        _dirty = false;
    }

    private void Apply(CareOutcome outcome)
    {
        if (outcome.Changed)
        {
            UpdateState(outcome.State);
        }
    }

    private void UpdateState(MendakoState next)
    {
        if (ReferenceEquals(next, State))
        {
            return;
        }

        State = next;
        _dirty = true;
        StateChanged?.Invoke(this, State);
    }

    private void SaveIfDirty()
    {
        if (_dirty)
        {
            SaveNow();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _advanceTimer.Stop();
        _autoSaveTimer.Stop();

        // 終了時は必ず書き出す。ここを省くと最大 3 分ぶんの世話が消える。
        try
        {
            SaveNow();
        }
        catch (Exception)
        {
            // 終了処理で例外を投げても得がない
        }
    }
}

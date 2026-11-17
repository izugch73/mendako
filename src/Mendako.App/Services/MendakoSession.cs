using System;
using System.IO;
using System.Linq;
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

    /// <summary>
    /// 1 回の <see cref="Advance"/> で、成長段階が上がった・長期間の不在から復帰した・アイテムを拾った (または拾えなかった)
    /// のいずれかがあったときに、まとめて 1 回だけ発火する。
    /// 通知の風船は後から出したものが前のものを置き換えるので、別々に知らせると最後の 1 つしか見えない。
    /// </summary>
    public event EventHandler<AdvanceReport>? Advanced;

    /// <summary>持ち物の上限。</summary>
    public int MaxInventoryItems => _config.MaxInventoryItems;

    /// <summary>タスクバーに同時に置ける数。</summary>
    public int MaxScatteredItems => _config.MaxScatteredItems;

    /// <summary>飾りの値段。売っていなければ null。</summary>
    public int? PriceOf(Accessory accessory) => Shop.PriceOf(accessory, _config);

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

        var foraged = outcome.Foraged.Count > 0 || outcome.ForageMissed > 0
            ? new ForageReport(outcome.Foraged.Select(f => f.Item).ToArray(), outcome.ForageMissed)
            : null;

        if (outcome.StageAdvancedTo is not null || outcome.TicksSkipped > 0 || foraged is not null)
        {
            Advanced?.Invoke(this, new AdvanceReport(outcome.StageAdvancedTo, outcome.TicksSkipped, foraged));
        }
    }

    /// <summary>持ち物を 1 個売る。</summary>
    public SellOutcome Sell(ItemId item)
    {
        // 売る直前までに拾った分を先に取り込む。持ち物の画面と実際の持ち物がずれたまま売らないため
        Advance();

        var outcome = Inventory.Sell(State, item);
        CommitIfChanged(outcome.Changed, outcome.State);

        return outcome;
    }

    /// <summary>持ち物のアイテムを 1 個、タスクバーの指定の場所に置く。</summary>
    public ScatterOutcome Scatter(ItemId item, double position, string? monitorId)
    {
        // 置く直前までに拾った分を先に取り込む。持ち物の画面と実際の持ち物がずれたまま置かないため
        Advance();

        var outcome = Scattering.Place(State, item, position, monitorId, _config);
        CommitIfChanged(outcome.Changed, outcome.State);

        return outcome;
    }

    /// <summary>置いてあるアイテムを持ち物に戻す。持ち物がいっぱいなら戻せない。</summary>
    public ScatterOutcome PickUp(Guid id)
    {
        Advance();

        var outcome = Scattering.PickUp(State, id, _config);
        CommitIfChanged(outcome.Changed, outcome.State);

        return outcome;
    }

    /// <summary>メンダコが置いてあるアイテムのそばに着いた。分類に応じた反応を状態に反映して返す。</summary>
    public ScatterReaction ReactToScattered(Guid id)
    {
        Advance();

        var outcome = Scattering.React(State, id, _clock.UtcNow, _config);
        CommitIfChanged(outcome.Changed, outcome.State);

        return outcome.Reaction;
    }

    /// <summary>飾りを買う。買っただけでは付けない。</summary>
    public PurchaseOutcome Buy(Accessory accessory)
    {
        Advance();

        var outcome = Shop.Buy(State, accessory, _config);
        CommitIfChanged(outcome.Changed, outcome.State);

        return outcome;
    }

    /// <summary>持っている飾りを付ける。<see cref="Accessory.None"/> を渡すと外す。</summary>
    public EquipOutcome Equip(Accessory accessory)
    {
        var outcome = Shop.Equip(State, accessory);
        CommitIfChanged(outcome.Changed, outcome.State);

        return outcome;
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

    /// <summary>
    /// かいがらの増減は、取り消せない操作の結果なので即座に書き出す。
    /// 失敗しても例外は伝えない。dirty のままなので自動保存が再試行するし、
    /// ここで落とすと売買の画面や自動保存のタイマーから例外が飛び出してしまう。
    /// </summary>
    private void TrySaveNow()
    {
        try
        {
            SaveNow();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>変わっていれば状態に反映して、すぐ書き出す。</summary>
    private void CommitIfChanged(bool changed, MendakoState next)
    {
        if (changed)
        {
            UpdateState(next);
            TrySaveNow();
        }
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
            TrySaveNow();
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

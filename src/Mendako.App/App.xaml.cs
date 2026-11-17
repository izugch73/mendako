using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using Mendako.Core.Behavior;
using Mendako.App.Services;
using Mendako.App.Views;
using Mendako.Core.Model;
using Mendako.Core.Simulation;
using Mendako.Platform;
using Microsoft.Win32;

namespace Mendako.App;

/// <summary>
/// コンポジションルート。各部品を組み立てて配線するだけで、ロジックは持たない。
/// </summary>
public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\Mendako.SingleInstance";

    private Mutex? _instanceMutex;
    private JsonFileStore<AppSettings>? _settingsStore;
    private MendakoSession? _session;
    private TrayController? _tray;
    private PetWindow? _window;
    private InventoryWindow? _inventoryWindow;
    private ShopWindow? _shopWindow;
    private AppSettings _settings = new();

    /// <summary>
    /// 持ち物がいっぱいで拾えなかったことを、もう知らせたか。
    /// 拾う機会は 3 時間ごとにあるので、いっぱいのまま放っておくと同じ通知が延々と出る。
    /// 持ち物に空きができたら (売ったら) 戻して、次にいっぱいになったときにまた知らせる。
    /// </summary>
    private bool _fullNoticeShown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 二重起動するとメンダコが 2 匹になり、保存も競合する
        _instanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        AppPaths.EnsureDataDirectory();

        _settingsStore = new JsonFileStore<AppSettings>(AppPaths.SettingsFile);
        _settings = _settingsStore.Load() ?? new AppSettings();

        var session = new MendakoSession();
        _session = session;
        session.StateChanged += OnStateChanged;
        session.StageAdvanced += OnStageAdvanced;
        session.ReturnedAfterAbsence += OnReturnedAfterAbsence;
        session.Foraged += OnForaged;

        var tray = new TrayController();
        _tray = tray;
        tray.FeedRequested += (_, _) => Feed();
        tray.PetRequested += (_, _) => Pet();
        tray.SleepToggleRequested += (_, _) => session.ToggleSleep();
        tray.StatusRequested += (_, _) => ShowStatus();
        tray.InventoryRequested += (_, _) => ShowInventory();
        tray.ShopRequested += (_, _) => ShowShop();
        tray.ExitRequested += (_, _) => Shutdown();
        tray.AutoStartChanged += OnAutoStartChanged;
        tray.HideOnFullScreenChanged += OnHideOnFullScreenChanged;
        tray.InitializeToggles(AutoStart.IsEnabled(), _settings.HideOnFullScreen);

        var window = new PetWindow();
        _window = window;
        window.FeedRequested += (_, _) => Feed();
        window.PetRequested += (_, _) => Pet();
        window.PokeRequested += (_, _) => Poke();
        window.HideOnFullScreenChanged += OnHideOnFullScreenChanged;
        window.SleepToggleRequested += (_, _) => session.ToggleSleep();
        window.InventoryRequested += (_, _) => ShowInventory();
        window.ScatterArrived += (_, id) => OnScatterArrived(id);
        window.ScatterPickUpRequested += (_, id) => OnScatterPickUpRequested(id);
        window.ShopRequested += (_, _) => ShowShop();
        window.ExitRequested += (_, _) => Shutdown();
        window.PlacementChanged += OnPlacementChanged;
        window.Initialize(session.State, _settings);
        window.Show();

        tray.UpdateState(session.State);

        // ログオフ・シャットダウンで取りこぼさないよう、ここで必ず保存する
        SystemEvents.SessionEnding += OnSessionEnding;

        // 起動時のキャッチアップはウィンドウを出してから走らせる。
        // 段階アップのリアクションを見せられるようにするため。
        session.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.SessionEnding -= OnSessionEnding;

        _window?.Close();
        _session?.Dispose();
        _tray?.Dispose();

        _instanceMutex?.Dispose();
        _instanceMutex = null;

        base.OnExit(e);
    }

    // --- お世話 ---

    private void Feed()
    {
        if (_session is null || _window is null)
        {
            return;
        }

        var action = _session.Feed() switch
        {
            CareResult.Accepted => PetAction.Eat,
            CareResult.RefusedTooFull => PetAction.Refuse,
            CareResult.RefusedAsleep => PetAction.Refuse,
            _ => PetAction.None,
        };

        if (action != PetAction.None)
        {
            _window.React(action);
        }
    }

    private void Pet()
    {
        if (_session is null || _window is null)
        {
            return;
        }

        var action = _session.Pet() switch
        {
            CareResult.Accepted => PetAction.Happy,
            CareResult.AcceptedWithoutGain => PetAction.Happy,
            CareResult.RefusedAsleep => PetAction.Refuse,
            _ => PetAction.None,
        };

        if (action != PetAction.None)
        {
            _window.React(action);
        }
    }

    /// <summary>つつく。育成には影響せず、びっくりするだけ。</summary>
    private void Poke()
    {
        if (_session is null || _window is null)
        {
            return;
        }

        _window.React(_session.State.IsAsleep ? PetAction.Refuse : PetAction.Startle);
    }

    // --- イベント配線 ---

    private void OnStateChanged(object? sender, MendakoState state)
    {
        _window?.UpdateState(state);
        _tray?.UpdateState(state);

        if (_session is not null && state.Items.Count < _session.MaxInventoryItems)
        {
            _fullNoticeShown = false;
        }
    }

    private void OnStageAdvanced(object? sender, GrowthStage stage)
    {
        _window?.React(PetAction.Evolve);

        var name = _session?.State.Name ?? "メンダコ";
        _tray?.ShowBalloon("おおきくなった", $"{name} は {GrowthStages.DisplayName(stage)} になりました。");
    }

    private void OnReturnedAfterAbsence(object? sender, long skippedTicks)
    {
        var days = Math.Max(1, skippedTicks / (24 * 60));
        _tray?.ShowBalloon(
            "おかえりなさい",
            $"{days} 日以上ぶりですね。そのあいだの時間は打ち切ってあるので、手遅れにはなっていません。");
    }

    private void OnForaged(object? sender, ForageReport report)
    {
        var lines = new List<string>();

        if (report.Items.Count > 0)
        {
            lines.Add(DescribeForaged(report.Items));
        }

        if (report.Missed > 0 && !_fullNoticeShown)
        {
            _fullNoticeShown = true;
            lines.Add("もちものが いっぱいで ひろえなかった。「もちもの」で うると また ひろってくるよ");
        }

        if (lines.Count == 0)
        {
            return;
        }

        // 閉じていた間にまとめて拾った分も、ここで 1 回の通知にする
        _tray?.ShowBalloon(
            report.Items.Count > 0 ? "ひろいもの" : "もちものが いっぱい",
            string.Join(Environment.NewLine, lines));
    }

    private static string DescribeForaged(IReadOnlyList<ItemId> items)
    {
        var kinds = items
            .GroupBy(id => id)
            .Select(group =>
            {
                var name = ItemCatalog.Get(group.Key).DisplayName;
                return group.Count() > 1 ? $"{name} ×{group.Count()}" : name;
            })
            .ToList();

        if (items.Count == 1)
        {
            return $"{kinds[0]} を拾ってきた";
        }

        // 長くなりすぎるとバルーンの字数に収まらない
        const int shown = 3;
        var list = string.Join("、", kinds.Take(shown)) + (kinds.Count > shown ? " ほか" : string.Empty);
        return $"{list} を拾ってきた (ぜんぶで {items.Count} こ)";
    }

    // --- 置いたアイテム ---

    /// <summary>メンダコが置いたアイテムに着いた。分類に応じて食べる・喜ぶ・つつく。</summary>
    private void OnScatterArrived(Guid id)
    {
        if (_session is null || _window is null)
        {
            return;
        }

        var action = _session.ReactToScattered(id) switch
        {
            ScatterReaction.Ate => PetAction.Eat,
            ScatterReaction.RefusedTooFull => PetAction.Refuse,
            ScatterReaction.Delighted => PetAction.Happy,
            ScatterReaction.Admired => PetAction.Admire,
            ScatterReaction.Poked => PetAction.Poke,
            _ => PetAction.None,
        };

        if (action != PetAction.None)
        {
            _window.React(action);
        }
    }

    private void OnScatterPickUpRequested(Guid id)
    {
        if (_session is null)
        {
            return;
        }

        if (_session.PickUp(id).Result == ScatterResult.InventoryFull)
        {
            _tray?.ShowBalloon("もちものが いっぱい", "もちものが いっぱいで もどせない。「もちもの」で うってから、もういちど さわってね");
        }
    }

    private void OnPlacementChanged(object? sender, PetPlacement placement)
    {
        _settings = _settings with { MonitorId = placement.MonitorId, PositionRatio = placement.Ratio };
        _settingsStore?.Save(_settings);
    }

    private void OnAutoStartChanged(object? sender, bool enabled)
    {
        try
        {
            AutoStart.Set(enabled, AppPaths.ExecutablePath);
        }
        catch (Exception ex)
        {
            _tray?.ShowBalloon("自動起動の設定に失敗しました", ex.Message);
        }
    }

    private void OnHideOnFullScreenChanged(object? sender, bool enabled)
    {
        _settings = _settings with { HideOnFullScreen = enabled };
        _settingsStore?.Save(_settings);
        _window?.UpdateSettings(_settings);

        // トレイと本体の右クリックメニュー、どちらで切り替えても両方のチェックを揃える
        _tray?.InitializeToggles(AutoStart.IsEnabled(), enabled);
    }

    // --- もちもの・おみせ ---

    private void ShowInventory()
    {
        if (_session is null)
        {
            return;
        }

        // 二重に開くと、売った結果が片方の窓にしか映らない時間ができて紛らわしい
        if (_inventoryWindow is null)
        {
            var window = new InventoryWindow(_session, () => _window?.ChooseScatterSpot());
            window.Closed += (_, _) => _inventoryWindow = null;
            _inventoryWindow = window;
            window.Show();
        }

        BringToFront(_inventoryWindow);
    }

    private void ShowShop()
    {
        if (_session is null)
        {
            return;
        }

        if (_shopWindow is null)
        {
            var window = new ShopWindow(_session);
            window.Closed += (_, _) => _shopWindow = null;
            _shopWindow = window;
            window.Show();
        }

        BringToFront(_shopWindow);
    }

    private static void BringToFront(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }

    private void OnSessionEnding(object? sender, SessionEndingEventArgs e) => _session?.SaveNow();

    private void ShowStatus()
    {
        if (_session is null || _tray is null)
        {
            return;
        }

        var state = _session.State;
        var mood = state.IsAsleep ? "すやすや ねている" : Moods.DisplayName(state.Mood);
        var growth = state.Stage == GrowthStage.Elder
            ? "もう じゅうぶん おおきい"
            : $"つぎの すがたまで {state.StageProgress * 100d:F0}%";

        _tray.ShowBalloon(
            $"{state.Name} ({GrowthStages.DisplayName(state.Stage)})",
            $"きぶん: {mood}\n" +
            $"おなか {state.Satiety:F0} / げんき {state.Energy:F0} / なつき {state.Affection:F0}\n" +
            growth);
    }
}

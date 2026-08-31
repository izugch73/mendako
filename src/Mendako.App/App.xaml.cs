using System;
using System.Threading;
using System.Windows;
using Mendako.App.Behavior;
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
    private AppSettings _settings = new();

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

        var tray = new TrayController();
        _tray = tray;
        tray.FeedRequested += (_, _) => Feed();
        tray.PetRequested += (_, _) => Pet();
        tray.SleepToggleRequested += (_, _) => session.ToggleSleep();
        tray.StatusRequested += (_, _) => ShowStatus();
        tray.ExitRequested += (_, _) => Shutdown();
        tray.AutoStartChanged += OnAutoStartChanged;
        tray.HideOnFullScreenChanged += OnHideOnFullScreenChanged;
        tray.InitializeToggles(AutoStart.IsEnabled(), _settings.HideOnFullScreen);

        var window = new PetWindow();
        _window = window;
        window.FeedRequested += (_, _) => Feed();
        window.PetRequested += (_, _) => Pet();
        window.SleepToggleRequested += (_, _) => session.ToggleSleep();
        window.ExitRequested += (_, _) => Shutdown();
        window.PositionRatioChanged += OnPositionRatioChanged;
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

    // --- イベント配線 ---

    private void OnStateChanged(object? sender, MendakoState state)
    {
        _window?.UpdateState(state);
        _tray?.UpdateState(state);
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

    private void OnPositionRatioChanged(object? sender, double ratio)
    {
        _settings = _settings with { PositionRatio = ratio };
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

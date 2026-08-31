using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Mendako.App.Behavior;
using Mendako.App.Services;
using Mendako.Core.Model;
using Mendako.Platform;
using Microsoft.Win32;

namespace Mendako.App.Views;

/// <summary>
/// タスクバーの上に住む透過オーバーレイ。
/// このアプリで一番ややこしいのがこのクラスなので、意図を細かめに書いてある。
/// </summary>
public partial class PetWindow : Window
{
    /// <summary>ステータスバーのトラック幅 (XAML と揃えること)。</summary>
    private const double TrackWidth = 156d;

    /// <summary>ウィンドウ下端をタスクバーにどれだけ沈めるか (DIP)。</summary>
    private const double SinkIntoTaskbar = 16d;

    /// <summary>これ以上動いたらクリックではなくドラッグとみなす (DIP)。</summary>
    private const double DragThreshold = 4d;

    private static readonly TimeSpan ActiveFrameInterval = TimeSpan.FromMilliseconds(33);

    /// <summary>就寝中など動きが乏しいときのフレーム間隔。常駐アプリなので回しっぱなしにしない。</summary>
    private static readonly TimeSpan CalmFrameInterval = TimeSpan.FromMilliseconds(140);

    private static readonly TimeSpan LayoutInterval = TimeSpan.FromSeconds(2);

    private readonly BehaviorMachine _behavior = new();
    private readonly DispatcherTimer _frameTimer;
    private readonly DispatcherTimer _layoutTimer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private IntPtr _hwnd;
    private DpiScale _dpi = new(1d, 1d);
    private MendakoState _state = MendakoState.CreateNew(DateTimeOffset.UtcNow);
    private AppSettings _settings = new();
    private TaskbarEdge _taskbarEdge = TaskbarEdge.Bottom;

    private double _lastFrameSeconds;
    private bool _clickThrough = true;
    private bool _hovering;
    private bool _hiddenForPresence;

    private bool _dragging;
    private bool _dragMoved;
    private bool _suppressClickAction;
    private (int X, int Y) _dragStartCursor;
    private double _dragStartLeft;
    private double _dragStartTop;

    public PetWindow()
    {
        InitializeComponent();

        _frameTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = ActiveFrameInterval };
        _frameTimer.Tick += OnFrame;

        _layoutTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = LayoutInterval };
        _layoutTimer.Tick += OnLayoutTick;
    }

    public event EventHandler? FeedRequested;

    public event EventHandler? PetRequested;

    public event EventHandler? SleepToggleRequested;

    public event EventHandler? ExitRequested;

    /// <summary>ドラッグで位置が変わったときに、新しい比率とともに発火する。</summary>
    public event EventHandler<double>? PositionRatioChanged;

    public void Initialize(MendakoState state, AppSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        UpdateState(state);
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        RefreshStatusCardVisibility();
        UpdatePosition();
    }

    /// <summary>最新の育成状態を反映する。</summary>
    public void UpdateState(MendakoState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));

        SleepMenuItem.Header = _state.IsAsleep ? "起こす" : "寝かせる";
        FeedMenuItem.IsEnabled = !_state.IsAsleep;
        PetMenuItem.IsEnabled = !_state.IsAsleep;

        RefreshStatusCard();
    }

    /// <summary>一時的なリアクションを再生する。</summary>
    public void React(PetAction action) => _behavior.Trigger(action);

    // --- ウィンドウ初期化 ---

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwnd = new WindowInteropHelper(this).Handle;

        // Alt+Tab に出さない、フォーカスも奪わない
        OverlayWindow.ApplyOverlayStyles(_hwnd);
        OverlayWindow.EnsureTopmost(_hwnd);
        OverlayWindow.SetClickThrough(_hwnd, true);

        _dpi = VisualTreeHelper.GetDpi(this);
        UpdatePosition();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        _lastFrameSeconds = _clock.Elapsed.TotalSeconds;
        _frameTimer.Start();
        _layoutTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _frameTimer.Stop();
        _layoutTimer.Stop();
        base.OnClosed(e);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        _dpi = newDpi;
        UpdatePosition();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // モニタの抜き差しや解像度変更でタスクバーの位置が変わる
        _dpi = VisualTreeHelper.GetDpi(this);
        UpdatePosition();
    }

    // --- 位置決め ---

    private void UpdatePosition()
    {
        if (_dragging)
        {
            return;
        }

        var anchor = ComputeAnchor();
        Left = anchor.Left;
        Top = anchor.Top;
    }

    private (double Left, double Top) ComputeAnchor()
    {
        var taskbar = TaskbarLocator.Locate();

        // 自動的に隠す設定だとタスクバーの矩形が画面外にあるので、作業領域を基準にする
        if (taskbar is null || taskbar.IsAutoHide)
        {
            _taskbarEdge = TaskbarEdge.Bottom;
            var work = SystemParameters.WorkArea;
            return (
                work.Left + (_settings.PositionRatio * Math.Max(0d, work.Width - Width)),
                work.Bottom - Height + SinkIntoTaskbar);
        }

        _taskbarEdge = taskbar.Edge;

        // タスクバーの座標は物理ピクセルなので DIP に直す
        var left = taskbar.Left / _dpi.DpiScaleX;
        var top = taskbar.Top / _dpi.DpiScaleY;
        var right = taskbar.Right / _dpi.DpiScaleX;
        var bottom = taskbar.Bottom / _dpi.DpiScaleY;
        var spanX = Math.Max(0d, (right - left) - Width);
        var spanY = Math.Max(0d, (bottom - top) - Height);

        return taskbar.Edge switch
        {
            TaskbarEdge.Top => (left + (_settings.PositionRatio * spanX), bottom - SinkIntoTaskbar),
            TaskbarEdge.Left => (right - SinkIntoTaskbar, top + (_settings.PositionRatio * spanY)),
            TaskbarEdge.Right => (left - Width + SinkIntoTaskbar, top + (_settings.PositionRatio * spanY)),
            _ => (left + (_settings.PositionRatio * spanX), top - Height + SinkIntoTaskbar),
        };
    }

    private bool IsHorizontalTaskbar => _taskbarEdge is TaskbarEdge.Bottom or TaskbarEdge.Top;

    private double ComputeRatioFromPosition()
    {
        var taskbar = TaskbarLocator.Locate();

        if (taskbar is null || taskbar.IsAutoHide)
        {
            var work = SystemParameters.WorkArea;
            return Clamp01((Left - work.Left) / Math.Max(1d, work.Width - Width));
        }

        var left = taskbar.Left / _dpi.DpiScaleX;
        var top = taskbar.Top / _dpi.DpiScaleY;
        var right = taskbar.Right / _dpi.DpiScaleX;
        var bottom = taskbar.Bottom / _dpi.DpiScaleY;

        return IsHorizontalTaskbar
            ? Clamp01((Left - left) / Math.Max(1d, (right - left) - Width))
            : Clamp01((Top - top) / Math.Max(1d, (bottom - top) - Height));
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0d, 1d);

    // --- フレーム更新 ---

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed.TotalSeconds;
        var delta = now - _lastFrameSeconds;
        _lastFrameSeconds = now;

        var pose = _behavior.Advance(delta, _state);
        Visual.Apply(pose, _state);

        UpdateHitTargeting();
        UpdateFrameRate();
    }

    /// <summary>動きが乏しいときはフレームレートを落とす。</summary>
    private void UpdateFrameRate()
    {
        var calm = _state.IsAsleep && _behavior.CurrentAction == PetAction.None && !_hovering;
        var desired = calm ? CalmFrameInterval : ActiveFrameInterval;

        if (_frameTimer.Interval != desired)
        {
            _frameTimer.Interval = desired;
        }
    }

    /// <summary>
    /// カーソルがメンダコの上にあるかを判定し、クリックスルーを切り替える。
    /// クリックスルーが有効なあいだ WPF はマウスイベントを受け取れないので、
    /// カーソル位置は Win32 から直接ポーリングする必要がある。
    /// </summary>
    private void UpdateHitTargeting()
    {
        if (_dragging)
        {
            return;
        }

        var cursor = Pointer.TryGetPosition();
        if (cursor is null)
        {
            return;
        }

        var x = (cursor.Value.X / _dpi.DpiScaleX) - Left;
        var y = (cursor.Value.Y / _dpi.DpiScaleY) - Top;

        var inside = x >= 0d && y >= 0d && x < Width && y < Height;
        var overContent = inside && IsOverContent(new Point(x, y));

        SetHovering(overContent);
        ApplyClickThrough(!overContent);
    }

    /// <summary>
    /// メンダコ本体（ドットのアルファ基準）かステータスカードの上にいるか。
    /// MendakoVisual は IsHitTestVisible を落としてあるので、
    /// VisualTreeHelper が拾うのはカードだけになる。
    /// </summary>
    private bool IsOverContent(Point point)
    {
        if (VisualTreeHelper.HitTest(RootGrid, point) is not null)
        {
            return true;
        }

        return Visual.HitTestSprite(RootGrid.TranslatePoint(point, Visual));
    }

    private void ApplyClickThrough(bool enabled)
    {
        if (_clickThrough == enabled)
        {
            return;
        }

        _clickThrough = enabled;
        OverlayWindow.SetClickThrough(_hwnd, enabled);
    }

    private void SetHovering(bool hovering)
    {
        if (_hovering == hovering)
        {
            return;
        }

        _hovering = hovering;
        RefreshStatusCardVisibility();
    }

    private void OnLayoutTick(object? sender, EventArgs e)
    {
        // タスクバー自身も TOPMOST なので、放っておくと順序が入れ替わることがある
        OverlayWindow.EnsureTopmost(_hwnd);
        UpdatePosition();
        UpdatePresenceVisibility();
    }

    /// <summary>全画面ゲームやプレゼン中は引っ込む。</summary>
    private void UpdatePresenceVisibility()
    {
        var shouldHide = _settings.HideOnFullScreen && UserPresence.ShouldHideOverlay();
        if (shouldHide == _hiddenForPresence)
        {
            return;
        }

        _hiddenForPresence = shouldHide;

        if (shouldHide)
        {
            _frameTimer.Stop();
            Hide();
        }
        else
        {
            Show();
            OverlayWindow.ApplyOverlayStyles(_hwnd);
            OverlayWindow.EnsureTopmost(_hwnd);
            _lastFrameSeconds = _clock.Elapsed.TotalSeconds;
            _frameTimer.Start();
        }
    }

    // --- マウス操作 ---

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (e.ClickCount == 2)
        {
            _suppressClickAction = true;
            FeedRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        var cursor = Pointer.TryGetPosition();
        if (cursor is null)
        {
            return;
        }

        _dragging = true;
        _dragMoved = false;
        _suppressClickAction = false;
        _dragStartCursor = cursor.Value;
        _dragStartLeft = Left;
        _dragStartTop = Top;

        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_dragging)
        {
            return;
        }

        var cursor = Pointer.TryGetPosition();
        if (cursor is null)
        {
            return;
        }

        var dx = (cursor.Value.X - _dragStartCursor.X) / _dpi.DpiScaleX;
        var dy = (cursor.Value.Y - _dragStartCursor.Y) / _dpi.DpiScaleY;

        if (!_dragMoved && (Math.Abs(dx) > DragThreshold || Math.Abs(dy) > DragThreshold))
        {
            _dragMoved = true;
        }

        if (!_dragMoved)
        {
            return;
        }

        // タスクバーに沿った方向にだけ動かす
        if (IsHorizontalTaskbar)
        {
            Left = _dragStartLeft + dx;
        }
        else
        {
            Top = _dragStartTop + dy;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        ReleaseMouseCapture();

        if (_dragMoved)
        {
            var ratio = ComputeRatioFromPosition();
            _settings = _settings with { PositionRatio = ratio };
            PositionRatioChanged?.Invoke(this, ratio);
            UpdatePosition();
        }
        else if (!_suppressClickAction)
        {
            PetRequested?.Invoke(this, EventArgs.Empty);
        }

        _suppressClickAction = false;
        e.Handled = true;
    }

    private void OnFeedClick(object sender, RoutedEventArgs e) => FeedRequested?.Invoke(this, EventArgs.Empty);

    private void OnPetClick(object sender, RoutedEventArgs e) => PetRequested?.Invoke(this, EventArgs.Empty);

    private void OnSleepToggleClick(object sender, RoutedEventArgs e) => SleepToggleRequested?.Invoke(this, EventArgs.Empty);

    private void OnExitClick(object sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);

    // --- ステータスカード ---

    private void RefreshStatusCardVisibility() =>
        StatusCard.Visibility = _hovering && _settings.ShowStatusOnHover
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void RefreshStatusCard()
    {
        NameText.Text = _state.Name;

        var mood = _state.IsAsleep ? "すやすや" : Moods.DisplayName(_state.Mood);
        SubtitleText.Text = $"{GrowthStages.DisplayName(_state.Stage)} / {mood}";

        SatietyBar.Width = TrackWidth * (_state.Satiety / 100d);
        EnergyBar.Width = TrackWidth * (_state.Energy / 100d);
        AffectionBar.Width = TrackWidth * (_state.Affection / 100d);

        GrowthText.Text = _state.Stage == GrowthStage.Elder
            ? "もう じゅうぶん おおきい"
            : $"つぎの すがたまで {_state.StageProgress * 100d:F0}%";
    }
}

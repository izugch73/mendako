using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Mendako.App.Services;
using Mendako.App.Sprites;
using Mendako.Core.Behavior;
using Mendako.Core.Model;
using Mendako.Core.Simulation;
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

    /// <summary>これより低い位置で放されたら、落とさずにその場へ置く (DIP)。</summary>
    private const double MinFallHeight = 12d;

    /// <summary>降りる速さの上限 (DIP / 秒)。傘を広げているので、すとんとは落ちない。</summary>
    private const double FallTerminalSpeed = 170d;

    private const double FallAcceleration = 420d;

    /// <summary>置くアイテムを、メンダコの中心からこれだけ離す (DIP)。体の半分より外で、泳いで寄るのが見える距離。</summary>
    private const double ScatterPlaceDistance = 190d;

    /// <summary>置いたアイテム同士の中心の間隔の下限 (DIP)。アイテムの幅より広く取って、重ならないようにする。</summary>
    private const double ScatterMinGap = 60d;

    private static readonly TimeSpan ActiveFrameInterval = TimeSpan.FromMilliseconds(33);

    /// <summary>就寝中など動きが乏しいときのフレーム間隔。常駐アプリなので回しっぱなしにしない。</summary>
    private static readonly TimeSpan CalmFrameInterval = TimeSpan.FromMilliseconds(140);

    private static readonly TimeSpan LayoutInterval = TimeSpan.FromSeconds(2);

    private readonly BehaviorMachine _behavior = new();
    private readonly StrokeDetector _strokes = new();
    private readonly DispatcherTimer _frameTimer;
    private readonly DispatcherTimer _layoutTimer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ScatterLayer _scatter;

    private IntPtr _hwnd;
    private DpiScale _dpi = new(1d, 1d);
    private MendakoState _state = MendakoState.CreateNew(DateTimeOffset.UtcNow);
    private AppSettings _settings = new();
    private Rail _rail = new(true, 0, 0, 0);
    private ScatterRail _scatterRail = new(true, 0, 0, 0);
    private TaskbarInfo? _taskbar;
    private IReadOnlyList<ScatteredItem> _shownScattered = Array.Empty<ScatteredItem>();

    private double _lastFrameSeconds;
    private bool _clickThrough = true;
    private bool _hovering;
    private bool _hiddenForPresence;

    private bool _traveled;
    private double _travelRemainder;

    private bool _falling;
    private double _fallSpeed;
    private (double X, double Y) _fallPosition;

    private bool _dragging;
    private bool _dragMoved;
    private bool _suppressClickAction;
    private (int X, int Y) _dragStartCursor;
    private (int X, int Y) _dragStartWindow;

    public PetWindow()
    {
        InitializeComponent();

        _frameTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = ActiveFrameInterval };
        _frameTimer.Tick += OnFrame;

        _layoutTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = LayoutInterval };
        _layoutTimer.Tick += OnLayoutTick;

        _scatter = new ScatterLayer(ComputeScatterRail, () => _settings.MonitorId);
        _scatter.PickUpRequested += (_, id) => ScatterPickUpRequested?.Invoke(this, id);
    }

    public event EventHandler? FeedRequested;

    public event EventHandler? PetRequested;

    /// <summary>クリックでつつかれた。</summary>
    public event EventHandler? PokeRequested;

    public event EventHandler? SleepToggleRequested;

    public event EventHandler? ExitRequested;

    public event EventHandler? InventoryRequested;

    /// <summary>メンダコが置いてあるアイテムのそばに着いた。どう反応するかは受け取った側が決める。</summary>
    public event EventHandler<Guid>? ScatterArrived;

    /// <summary>置いてあるアイテムがクリックされた。</summary>
    public event EventHandler<Guid>? ScatterPickUpRequested;

    public event EventHandler? ShopRequested;

    /// <summary>右クリックメニューで「全画面アプリ中は隠す」を切り替えた。</summary>
    public event EventHandler<bool>? HideOnFullScreenChanged;

    /// <summary>ドラッグで居場所が変わったときに発火する。</summary>
    public event EventHandler<PetPlacement>? PlacementChanged;

    public void Initialize(MendakoState state, AppSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        HideOnFullScreenMenuItem.IsChecked = _settings.HideOnFullScreen;
        UpdateState(state);
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        HideOnFullScreenMenuItem.IsChecked = _settings.HideOnFullScreen;
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

        // 起動時はフレームタイマーが回り出す前に呼ばれる。ここで渡しておかないと、最初に描くコマだけ飾りが無い
        Visual.Accessory = _state.EquippedAccessory;

        RefreshStatusCard();
        SyncScattered();
    }

    /// <summary>置いてあるアイテムの窓を状態に合わせる。ハンドルができる前は、OnSourceInitialized が初回を受け持つ。</summary>
    private void SyncScattered()
    {
        if (_hwnd == IntPtr.Zero || _state.ScatteredItems.SequenceEqual(_shownScattered))
        {
            return;
        }

        _shownScattered = _state.ScatteredItems.ToArray();
        _scatter.Sync(_shownScattered);
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
        SyncScattered();

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
        _scatter.CloseAll();
        base.OnClosed(e);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        _dpi = newDpi;

        // DPI の違うモニタへ動かした SetWindowPos の最中に呼ばれるので、その場では動かさず後回しにする。
        // ウィンドウの実寸が変わったぶん、足元を合わせ直す必要がある
        Dispatcher.BeginInvoke(UpdatePosition, DispatcherPriority.Background);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // モニタの抜き差しや解像度変更でタスクバーの位置が変わる
        _dpi = VisualTreeHelper.GetDpi(this);
        UpdatePosition();
        _scatter.Relayout();
    }

    // --- 位置決め ---
    //
    // ここから下の座標はすべて物理ピクセル。DPI の違うモニタをまたぐので DIP には直さない。

    /// <summary>
    /// メンダコが行き来できる線分。横向きなら X が Start〜End、Y が Cross で固定。縦向きはその逆。
    /// </summary>
    private readonly record struct Rail(bool Horizontal, int Start, int End, int Cross)
    {
        public int Span(int size) => Math.Max(0, End - Start - size);
    }

    private (int Width, int Height) WindowPixelSize =>
        ((int)Math.Round(Width * _dpi.DpiScaleX), (int)Math.Round(Height * _dpi.DpiScaleY));

    private void UpdatePosition()
    {
        if (_dragging || _falling || _hwnd == IntPtr.Zero)
        {
            return;
        }

        var (x, y) = ComputeHome();

        // 2 秒ごとに呼ばれるので、動いていないときは触らない
        if (OverlayWindow.GetBounds(_hwnd) is { } bounds && bounds.Left == x && bounds.Top == y)
        {
            return;
        }

        OverlayWindow.MoveTo(_hwnd, x, y);
    }

    /// <summary>設定どおりの居場所（ウィンドウ左上）を求める。足場の線分も測り直す。</summary>
    private (int X, int Y) ComputeHome()
    {
        var (width, height) = WindowPixelSize;
        _taskbar = TaskbarLocator.Locate(_settings.MonitorId);
        _rail = ComputeRail(_taskbar, width, height);
        _scatterRail = ComputeScatterRail(_taskbar, _dpi);

        var along = _rail.Start + (int)Math.Round(_settings.PositionRatio * _rail.Span(_rail.Horizontal ? width : height));
        return _rail.Horizontal ? (along, _rail.Cross) : (_rail.Cross, along);
    }

    private Rail ComputeRail(TaskbarInfo? taskbar, int width, int height) => ComputeRail(taskbar, width, height, _dpi);

    private Rail ComputeRail(TaskbarInfo? taskbar, int width, int height, DpiScale dpi)
    {
        var sinkX = (int)Math.Round(SinkIntoTaskbar * dpi.DpiScaleX);
        var sinkY = (int)Math.Round(SinkIntoTaskbar * dpi.DpiScaleY);

        // 自動的に隠す設定だとタスクバーの矩形が画面外にあるので、そのモニタの作業領域の下端に立たせる
        if (taskbar is null || taskbar.IsAutoHide)
        {
            var work = taskbar?.WorkArea
                ?? TaskbarLocator.PrimaryWorkArea()
                ?? new PixelRect(0, 0, width, height);

            return new Rail(true, work.Left, work.Right, work.Bottom - height + sinkY);
        }

        var bar = taskbar.Bounds;

        return taskbar.Edge switch
        {
            TaskbarEdge.Top => new Rail(true, bar.Left, bar.Right, bar.Bottom - sinkY),
            TaskbarEdge.Left => new Rail(false, bar.Top, bar.Bottom, bar.Right - sinkX),
            TaskbarEdge.Right => new Rail(false, bar.Top, bar.Bottom, bar.Left - width + sinkX),
            _ => new Rail(true, bar.Left, bar.Right, bar.Top - height + sinkY),
        };
    }

    /// <summary>
    /// ドラッグを離した位置から、どのモニタのどこに居着くかを決める。
    /// ウィンドウの中心があるモニタにタスクバーがあればそこへ移り、無ければ今のモニタに留まる。
    /// </summary>
    private PetPlacement ComputePlacementFromPosition()
    {
        if (OverlayWindow.GetBounds(_hwnd) is not { } bounds)
        {
            return new PetPlacement(_settings.MonitorId, _settings.PositionRatio);
        }

        var centreX = bounds.Left + (bounds.Width / 2);
        var centreY = bounds.Top + (bounds.Height / 2);

        var taskbars = TaskbarLocator.LocateAll();
        var target = TaskbarLocator.Locate(_settings.MonitorId);
        foreach (var candidate in taskbars)
        {
            if (candidate.MonitorBounds.Contains(centreX, centreY))
            {
                target = candidate;
                break;
            }
        }

        var rail = ComputeRail(target, bounds.Width, bounds.Height);
        var position = rail.Horizontal ? bounds.Left : bounds.Top;
        var span = Math.Max(1, rail.Span(rail.Horizontal ? bounds.Width : bounds.Height));
        var ratio = Math.Clamp((position - rail.Start) / (double)span, 0d, 1d);

        // プライマリは名前で覚えない。プライマリを別のモニタに切り替えたときに付いていけるように
        var monitorId = target is null || target.IsPrimary ? null : target.MonitorId;
        return new PetPlacement(monitorId, ratio);
    }

    // --- フレーム更新 ---

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed.TotalSeconds;
        var delta = now - _lastFrameSeconds;
        _lastFrameSeconds = now;

        var pose = _behavior.Advance(delta, _state, MeasureSurroundings());
        Visual.Apply(pose, _state);
        Travel(pose.TravelDots);

        if (_behavior.TakeArrival() is { } arrived)
        {
            ScatterArrived?.Invoke(this, arrived);
        }
        StepFall(delta);

        UpdateHitTargeting();
        UpdateFrameRate();
    }

    /// <summary>1 ドットが物理ピクセルでいくつか。タスクバーに沿った方向の倍率で測る。</summary>
    private double DotPixels =>
        MendakoSprites.PixelScale(_state.Stage) * (_rail.Horizontal ? _dpi.DpiScaleX : _dpi.DpiScaleY);

    /// <summary>カーソルの位置と、左右にどれだけ動けるかを測る。</summary>
    private BehaviorInput MeasureSurroundings()
    {
        if (OverlayWindow.GetBounds(_hwnd) is not { } bounds)
        {
            return default;
        }

        var position = _rail.Horizontal ? bounds.Left : bounds.Top;
        var end = _rail.Start + _rail.Span(_rail.Horizontal ? bounds.Width : bounds.Height);

        (double X, double Y)? cursorDots = null;
        if (Pointer.TryGetPosition() is { } cursor)
        {
            var centre = Visual.TranslatePoint(Visual.SpriteCentre, RootGrid);
            var scale = MendakoSprites.PixelScale(_state.Stage);
            cursorDots = (
                (((cursor.X - bounds.Left) / _dpi.DpiScaleX) - centre.X) / scale,
                (((cursor.Y - bounds.Top) / _dpi.DpiScaleY) - centre.Y) / scale);
        }

        return new BehaviorInput
        {
            CursorDots = cursorDots,
            Hovering = _hovering || _dragging,
            RoomBeforeDots = (position - _rail.Start) / DotPixels,
            RoomAfterDots = (end - position) / DotPixels,
            VerticalRail = !_rail.Horizontal,
            Scattered = _scatter.Measure(
                _taskbar,
                _scatterRail,
                position + RestCentrePixels,
                DotPixels),
        };
    }

    /// <summary>
    /// 窓の端から、体の中心までの距離 (物理ピクセル、足場に沿った向き)。
    /// 揺れや跳ねを含まない位置なので、アイテムまでの距離を安定して測れる。
    /// </summary>
    private double RestCentrePixels
    {
        get
        {
            var centre = Visual.TranslatePoint(Visual.RestCentre, RootGrid);
            return _rail.Horizontal ? centre.X * _dpi.DpiScaleX : centre.Y * _dpi.DpiScaleY;
        }
    }

    /// <summary>
    /// アイテムを置く足場。メンダコの足場と同じ計算で、足元の高さ (縦置きなら体の中心の X) を合わせる。
    /// 倍率を引数にするのは、別のモニタに置いたアイテムがそのモニタの倍率で決めるため。
    /// </summary>
    private ScatterRail ComputeScatterRail(TaskbarInfo? taskbar, DpiScale dpi)
    {
        var width = (int)Math.Round(Width * dpi.DpiScaleX);
        var height = (int)Math.Round(Height * dpi.DpiScaleY);
        var rail = ComputeRail(taskbar, width, height, dpi);

        if (rail.Horizontal)
        {
            var footLine = Height - Visual.Height + MendakoVisual.FootY;
            return new ScatterRail(true, rail.Start, rail.End, rail.Cross + (int)Math.Round(footLine * dpi.DpiScaleY));
        }

        return new ScatterRail(false, rail.Start, rail.End, rail.Cross + (int)Math.Round(Width / 2d * dpi.DpiScaleX));
    }

    /// <summary>
    /// アイテムを置く場所を決める。メンダコから少し離れた左右どちらかで、置いてあるものと重ならない所。
    /// メンダコが泳いで行ける範囲に収めるので、置いたのに寄ってこない場所にはならない。
    /// </summary>
    public ScatterSpot? ChooseScatterSpot()
    {
        if (_hwnd == IntPtr.Zero || OverlayWindow.GetBounds(_hwnd) is not { } bounds)
        {
            return null;
        }

        var taskbar = TaskbarLocator.Locate(_settings.MonitorId);
        var rail = ComputeScatterRail(taskbar, _dpi);
        var scale = rail.Horizontal ? _dpi.DpiScaleX : _dpi.DpiScaleY;

        var centreOffset = RestCentrePixels;
        var windowLength = rail.Horizontal ? bounds.Width : bounds.Height;
        var petCentre = (rail.Horizontal ? bounds.Left : bounds.Top) + centreOffset;

        var centre = Scattering.ChoosePosition(
            petCentre,
            rail.Start + centreOffset,
            rail.End - (windowLength - centreOffset),
            _scatter.CentresOn(taskbar, rail),
            ScatterPlaceDistance * scale,
            ScatterMinGap * scale,
            Random.Shared);

        var monitorId = taskbar is null || taskbar.IsPrimary ? null : taskbar.MonitorId;
        return new ScatterSpot(monitorId, Math.Clamp((centre - rail.Start) / rail.Length, 0d, 1d));
    }

    /// <summary>
    /// おさんぽ。ウィンドウごとタスクバーに沿って動かす。
    /// 設定ファイルへの保存は泳ぎ終わってからの 1 回だけにする。
    /// </summary>
    private void Travel(double dots)
    {
        if (_traveled && _behavior.CurrentAction is not (PetAction.Swim or PetAction.Approach))
        {
            _traveled = false;
            _travelRemainder = 0d;
            PlacementChanged?.Invoke(this, new PetPlacement(_settings.MonitorId, _settings.PositionRatio));
        }

        if (dots == 0d || _dragging || _falling || OverlayWindow.GetBounds(_hwnd) is not { } bounds)
        {
            return;
        }

        // 1 フレームの移動は 1 ピクセルに満たないことが多いので、端数を持ち越す
        var pixels = (dots * DotPixels) + _travelRemainder;
        var whole = (int)Math.Truncate(pixels);
        _travelRemainder = pixels - whole;

        if (whole == 0)
        {
            return;
        }

        var span = _rail.Span(_rail.Horizontal ? bounds.Width : bounds.Height);
        var current = _rail.Horizontal ? bounds.Left : bounds.Top;
        var next = Math.Clamp(current + whole, _rail.Start, _rail.Start + span);

        if (_rail.Horizontal)
        {
            OverlayWindow.MoveTo(_hwnd, next, _rail.Cross);
        }
        else
        {
            OverlayWindow.MoveTo(_hwnd, _rail.Cross, next);
        }

        // 比率も一緒に進めておかないと、次の UpdatePosition で元の場所へ引き戻される
        _settings = _settings with { PositionRatio = span > 0 ? (next - _rail.Start) / (double)span : 0d };
        _traveled = true;
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
        var cursor = Pointer.TryGetPosition();

        // 置いたアイテムのクリック判定もここで一緒に回す。メンダコをつまんでいる間は受けない
        _scatter.UpdateHitTargeting(cursor, !_dragging);

        if (_dragging || cursor is null)
        {
            return;
        }

        if (OverlayWindow.GetBounds(_hwnd) is not { } bounds)
        {
            return;
        }

        var x = (cursor.Value.X - bounds.Left) / _dpi.DpiScaleX;
        var y = (cursor.Value.Y - bounds.Top) / _dpi.DpiScaleY;

        var inside = x >= 0d && y >= 0d && x < Width && y < Height;
        var point = new Point(x, y);

        // 本体はドットのアルファで判定する。なで判定に使うので、カードとは分けて持つ。
        // VisualTreeHelper は本体 (MendakoVisual.HitTestCore) とステータスカードの両方を拾う
        var overSprite = inside && Visual.HitTestSprite(RootGrid.TranslatePoint(point, Visual));
        var overContent = overSprite || (inside && VisualTreeHelper.HitTest(RootGrid, point) is not null);

        SetHovering(overContent);
        ApplyClickThrough(!overContent);

        // ボタンを押さずに体の上を左右に往復したら、なでたことにする
        if (_strokes.Update(_clock.Elapsed.TotalSeconds, x, overSprite))
        {
            PetRequested?.Invoke(this, EventArgs.Empty);
        }
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
        _scatter.Relayout();

        // メンダコの窓を貼り直したあとに貼ると、アイテムが手前に来る。逆だと、近寄ったときに体がアイテムを隠す
        _scatter.EnsureTopmost();
        UpdatePresenceVisibility();
    }

    /// <summary>全画面ゲームやプレゼン中は引っ込む。ゲームが別のモニタにいるなら、そのまま居てよい。</summary>
    private void UpdatePresenceVisibility()
    {
        var shouldHide = _settings.HideOnFullScreen && UserPresence.ShouldHideOverlay(_hwnd);
        if (shouldHide == _hiddenForPresence)
        {
            return;
        }

        _hiddenForPresence = shouldHide;

        if (shouldHide)
        {
            _scatter.SetHidden(true);
            _frameTimer.Stop();
            Hide();
        }
        else
        {
            Show();
            OverlayWindow.ApplyOverlayStyles(_hwnd);
            OverlayWindow.EnsureTopmost(_hwnd);

            // アイテムは本体より後に前面へ出す。先だとメンダコの下になる
            _scatter.SetHidden(false);
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
        if (cursor is null || OverlayWindow.GetBounds(_hwnd) is not { } bounds)
        {
            return;
        }

        // 落ちている最中でも空中でつかまえられる
        StopFalling();

        _dragging = true;
        _dragMoved = false;
        _suppressClickAction = false;
        _dragStartCursor = cursor.Value;
        _dragStartWindow = (bounds.Left, bounds.Top);

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

        var dx = cursor.Value.X - _dragStartCursor.X;
        var dy = cursor.Value.Y - _dragStartCursor.Y;

        if (!_dragMoved
            && (Math.Abs(dx) > DragThreshold * _dpi.DpiScaleX || Math.Abs(dy) > DragThreshold * _dpi.DpiScaleY))
        {
            _dragMoved = true;
            _behavior.Trigger(PetAction.Held);
        }

        if (!_dragMoved)
        {
            return;
        }

        // つまんでいるあいだは好きなところへ運べる。別のモニタへ持っていってもよい
        OverlayWindow.MoveTo(_hwnd, _dragStartWindow.X + dx, _dragStartWindow.Y + dy);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (!_dragging)
        {
            return;
        }

        var clicked = !_dragMoved && !_suppressClickAction;
        EndDrag();

        if (clicked)
        {
            PokeRequested?.Invoke(this, EventArgs.Empty);
        }

        _suppressClickAction = false;
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);

        // ボタンを離す前にキャプチャを奪われることがある (UAC の昇格画面、Win+L など)。
        // ここで手を離したことにしないと、つままれたまま宙に取り残される
        EndDrag();
    }

    private void EndDrag()
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        ReleaseMouseCapture();

        if (!_dragMoved)
        {
            return;
        }

        _dragMoved = false;
        _behavior.Stop(PetAction.Held);

        var placement = ComputePlacementFromPosition();
        _settings = _settings with { MonitorId = placement.MonitorId, PositionRatio = placement.Ratio };
        PlacementChanged?.Invoke(this, placement);

        StartFalling();
    }

    // --- 落下 ---

    /// <summary>
    /// 放された場所から足場へ戻る。持ち上げられていればふわふわ降り、ほとんど浮いていなければその場に置く。
    /// 「落下」と呼んでいるが、タスクバーが上や左右にある場合はそちらへ寄っていく。
    /// </summary>
    private void StartFalling()
    {
        if (OverlayWindow.GetBounds(_hwnd) is not { } bounds)
        {
            return;
        }

        var (homeX, homeY) = ComputeHome();
        var lift = _rail.Horizontal ? Math.Abs(bounds.Top - homeY) : Math.Abs(bounds.Left - homeX);

        if (lift <= MinFallHeight * (_rail.Horizontal ? _dpi.DpiScaleY : _dpi.DpiScaleX))
        {
            UpdatePosition();
            return;
        }

        _falling = true;
        _fallSpeed = 0d;
        _fallPosition = (bounds.Left, bounds.Top);
        _behavior.Trigger(PetAction.Fall);
    }

    private void StopFalling()
    {
        if (!_falling)
        {
            return;
        }

        _falling = false;
        _behavior.Stop(PetAction.Fall);
    }

    private void StepFall(double deltaSeconds)
    {
        if (!_falling)
        {
            return;
        }

        // 落ちている途中で DPI の違うモニタに入るとウィンドウの実寸が変わるので、行き先は毎フレーム測り直す
        var (homeX, homeY) = ComputeHome();

        _fallSpeed = Math.Min(FallTerminalSpeed, _fallSpeed + (FallAcceleration * deltaSeconds));
        var step = _fallSpeed * deltaSeconds * (_rail.Horizontal ? _dpi.DpiScaleY : _dpi.DpiScaleX);

        // 足場へ向かう方向はゆっくり、足場に沿った方向（端からはみ出して放したとき）は少し速めに寄せる
        var (stepX, stepY) = _rail.Horizontal ? (step * 1.5d, step) : (step, step * 1.5d);
        _fallPosition = (Approach(_fallPosition.X, homeX, stepX), Approach(_fallPosition.Y, homeY, stepY));

        var x = (int)Math.Round(_fallPosition.X);
        var y = (int)Math.Round(_fallPosition.Y);
        OverlayWindow.MoveTo(_hwnd, x, y);

        if (x == homeX && y == homeY)
        {
            _falling = false;
            _behavior.Stop(PetAction.Fall);
            _behavior.Trigger(PetAction.Land);
        }
    }

    private static double Approach(double current, double target, double maxStep) =>
        Math.Abs(target - current) <= maxStep
            ? target
            : current + (Math.Sign(target - current) * maxStep);

    private void OnFeedClick(object sender, RoutedEventArgs e) => FeedRequested?.Invoke(this, EventArgs.Empty);

    private void OnPetClick(object sender, RoutedEventArgs e) => PetRequested?.Invoke(this, EventArgs.Empty);

    private void OnSleepToggleClick(object sender, RoutedEventArgs e) => SleepToggleRequested?.Invoke(this, EventArgs.Empty);

    private void OnInventoryClick(object sender, RoutedEventArgs e) => InventoryRequested?.Invoke(this, EventArgs.Empty);

    private void OnShopClick(object sender, RoutedEventArgs e) => ShopRequested?.Invoke(this, EventArgs.Empty);

    private void OnExitClick(object sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);

    private void OnHideOnFullScreenClick(object sender, RoutedEventArgs e) =>
        HideOnFullScreenChanged?.Invoke(this, HideOnFullScreenMenuItem.IsChecked);

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

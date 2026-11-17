using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Mendako.Core.Behavior;
using Mendako.Core.Model;
using Mendako.Platform;

namespace Mendako.App.Views;

/// <summary>
/// アイテムを置ける線分。座標はすべて物理ピクセルで、メンダコの <c>PetWindow.Rail</c> と同じ向きの取り方をする。
/// </summary>
/// <param name="Ground">
/// 横向きなら、アイテムの底を合わせる Y (メンダコの足元と同じ高さ)。縦向きなら、アイテムの中心を合わせる X。
/// </param>
internal readonly record struct ScatterRail(bool Horizontal, int Start, int End, int Ground)
{
    public int Length => Math.Max(1, End - Start);
}

/// <summary>
/// 置いたアイテムのウィンドウをまとめて持つ。
/// どのモニタのどこに置くかは状態 (<see cref="ScatteredItem"/>) が持っていて、ここは映すだけ。
/// DPI はモニタごとに違うので、位置はウィンドウごとに自分のいるモニタの倍率で決め直す。
/// </summary>
internal sealed class ScatterLayer
{
    private readonly Func<TaskbarInfo?, DpiScale, ScatterRail> _railFor;
    private readonly Func<string?> _petMonitorId;
    private readonly Dictionary<Guid, ScatterItemWindow> _windows = new();

    private IReadOnlyList<TaskbarInfo> _taskbars = Array.Empty<TaskbarInfo>();
    private bool _hidden;

    /// <param name="railFor">タスクバーと倍率から足場を求める。メンダコの足場と同じ計算を使うため、本体の窓が持つ。</param>
    /// <param name="petMonitorId">メンダコのいるモニタ。置いたモニタが無くなったアイテムの避難先になる。</param>
    public ScatterLayer(Func<TaskbarInfo?, DpiScale, ScatterRail> railFor, Func<string?> petMonitorId)
    {
        _railFor = railFor ?? throw new ArgumentNullException(nameof(railFor));
        _petMonitorId = petMonitorId ?? throw new ArgumentNullException(nameof(petMonitorId));
    }

    /// <summary>置いたアイテムがクリックされた。持ち物に戻したい。</summary>
    public event EventHandler<Guid>? PickUpRequested;

    /// <summary>置いてあるものを状態に合わせる。増えたものは出し、無くなったものは薄くして消す。</summary>
    public void Sync(IReadOnlyList<ScatteredItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var wanted = items.Select(item => item.Id).ToHashSet();
        foreach (var id in _windows.Keys.Where(id => !wanted.Contains(id)).ToList())
        {
            _windows[id].FadeOutAndClose();
            _windows.Remove(id);
        }

        _taskbars = TaskbarLocator.LocateAll();

        foreach (var item in items)
        {
            if (_windows.TryGetValue(item.Id, out var existing))
            {
                existing.Item = item;
                Place(existing);
                continue;
            }

            var window = new ScatterItemWindow(item);
            window.Prepare();
            window.Clicked += (_, _) => PickUpRequested?.Invoke(this, window.Item.Id);
            window.DpiScaleChanged += (_, _) => Place(window);
            _windows[item.Id] = window;

            // 表示する前に置く場所へ動かす。先に出すと、別のモニタへ置くときに原点で一瞬光る
            Place(window);
            if (!_hidden)
            {
                window.ShowFaded();
            }
        }
    }

    /// <summary>タスクバーの位置を測り直して置き直す。タスクバーやモニタの構成は後から変わる。</summary>
    public void Relayout()
    {
        _taskbars = TaskbarLocator.LocateAll();

        foreach (var window in _windows.Values)
        {
            Place(window);
        }
    }

    /// <summary>タスクバー自身も最前面なので、順序を貼り直す。メンダコの窓を貼り直したあとに呼んで、アイテムを手前に重ねる。</summary>
    public void EnsureTopmost()
    {
        foreach (var window in _windows.Values)
        {
            OverlayWindow.EnsureTopmost(window.Handle);
        }
    }

    /// <summary>全画面アプリのあいだは本体と一緒に隠す。</summary>
    public void SetHidden(bool hidden)
    {
        if (_hidden == hidden)
        {
            return;
        }

        _hidden = hidden;

        foreach (var window in _windows.Values)
        {
            if (hidden)
            {
                window.Hide();
            }
            else
            {
                window.ShowFaded();
            }
        }
    }

    /// <summary>
    /// カーソルが絵のあるドットの上にあるアイテムだけ、クリックを受ける。それ以外はクリックスルー。
    /// </summary>
    /// <param name="enabled">false なら全部クリックスルーにする。メンダコをつまんでいる間など。</param>
    public void UpdateHitTargeting((int X, int Y)? cursor, bool enabled)
    {
        foreach (var window in _windows.Values)
        {
            var over = enabled && cursor is { } c && window.IsSolidAt(c.X, c.Y);
            window.SetClickThrough(!over);
        }
    }

    /// <summary>
    /// メンダコと同じ足場にあるアイテムを、メンダコの中心から見た位置で返す。
    /// 別のモニタにあるものは泳いでいけないので含めない。
    /// </summary>
    /// <param name="petTaskbar">メンダコのいるタスクバー。</param>
    /// <param name="petCentre">メンダコの中心の、足場に沿った座標 (物理ピクセル)。</param>
    /// <param name="dotPixels">メンダコの 1 ドットが物理ピクセルでいくつか。</param>
    public IReadOnlyList<ScatterTarget> Measure(TaskbarInfo? petTaskbar, ScatterRail rail, double petCentre, double dotPixels)
    {
        if (_windows.Count == 0 || dotPixels <= 0d)
        {
            return Array.Empty<ScatterTarget>();
        }

        var targets = new List<ScatterTarget>(_windows.Count);
        foreach (var window in _windows.Values)
        {
            if (!SameMonitor(Resolve(window.Item.MonitorId), petTaskbar))
            {
                continue;
            }

            var centre = CentreAlong(window.Item, rail, SizePixels(window, rail.Horizontal));
            targets.Add(new ScatterTarget(window.Item.Id, (centre - petCentre) / dotPixels));
        }

        return targets;
    }

    /// <summary>メンダコと同じ足場に置いてあるアイテムの中心 (足場に沿った座標、物理ピクセル)。置く場所を避けるのに使う。</summary>
    public IReadOnlyList<double> CentresOn(TaskbarInfo? petTaskbar, ScatterRail rail) =>
        _windows.Values
            .Where(window => SameMonitor(Resolve(window.Item.MonitorId), petTaskbar))
            .Select(window => (double)CentreAlong(window.Item, rail, SizePixels(window, rail.Horizontal)))
            .ToList();

    public void CloseAll()
    {
        foreach (var window in _windows.Values)
        {
            window.Close();
        }

        _windows.Clear();
    }

    private void Place(ScatterItemWindow window)
    {
        var taskbar = Resolve(window.Item.MonitorId);

        // 別の倍率のモニタへ動かすと、SetWindowPos の最中に倍率が変わる。その倍率で置き直す
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var dpi = window.Dpi;
            var rail = _railFor(taskbar, dpi);
            var size = SizePixels(window, rail.Horizontal);
            var centre = CentreAlong(window.Item, rail, size);

            // アイコンの絵の下にある空きのぶん下げて、絵の底を足元に合わせる
            var padding = (int)Math.Round(window.IconArt.BottomPaddingDots * size / (double)ScatterIcons.Dots);

            var (x, y) = rail.Horizontal
                ? (centre - (size / 2), rail.Ground - size + padding)
                : (rail.Ground - (size / 2), centre - (size / 2));

            // 2 秒ごとに置き直すので、動いていないときは触らない
            if (OverlayWindow.GetBounds(window.Handle) is not { } bounds || bounds.Left != x || bounds.Top != y)
            {
                OverlayWindow.MoveTo(window.Handle, x, y);
            }

            if (window.Dpi.DpiScaleX == dpi.DpiScaleX && window.Dpi.DpiScaleY == dpi.DpiScaleY)
            {
                break;
            }
        }
    }

    private static int SizePixels(ScatterItemWindow window, bool horizontal) =>
        (int)Math.Round(ScatterItemWindow.SizeDips * (horizontal ? window.Dpi.DpiScaleX : window.Dpi.DpiScaleY));

    /// <summary>アイテムの中心 (足場に沿った座標)。端に置いても窓が足場からはみ出さないように寄せる。</summary>
    private static int CentreAlong(ScatteredItem item, ScatterRail rail, int size)
    {
        var half = size / 2;
        var centre = rail.Start + (int)Math.Round(item.Position * rail.Length);
        return Math.Clamp(centre, rail.Start + half, Math.Max(rail.Start + half, rail.End - half));
    }

    /// <summary>
    /// アイテムを置いたタスクバー。置いたモニタが無くなっていたら、メンダコのいるタスクバーへ寄せる。
    /// 無くなったモニタの足場に置いたままにすると、画面の外で触れなくなる。
    /// </summary>
    private TaskbarInfo? Resolve(string? monitorId)
    {
        if (_taskbars.Count == 0)
        {
            return null;
        }

        var found = monitorId is null
            ? _taskbars.FirstOrDefault(taskbar => taskbar.IsPrimary)
            : _taskbars.FirstOrDefault(taskbar =>
                string.Equals(taskbar.MonitorId, monitorId, StringComparison.OrdinalIgnoreCase));
        if (found is not null)
        {
            return found;
        }

        var pet = _petMonitorId();
        return (pet is null ? null : _taskbars.FirstOrDefault(taskbar =>
                string.Equals(taskbar.MonitorId, pet, StringComparison.OrdinalIgnoreCase)))
            ?? _taskbars[0];
    }

    private static bool SameMonitor(TaskbarInfo? a, TaskbarInfo? b) =>
        string.Equals(a?.MonitorId, b?.MonitorId, StringComparison.OrdinalIgnoreCase);
}

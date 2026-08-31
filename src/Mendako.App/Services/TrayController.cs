using System;
using System.Drawing;
using System.Windows.Forms;
using Mendako.Core.Model;

namespace Mendako.App.Services;

/// <summary>
/// タスクトレイの常駐アイコンとメニュー。操作はイベントで外に投げるだけで、
/// 育成ロジックには触らない。
/// </summary>
public sealed class TrayController : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _feedItem;
    private readonly ToolStripMenuItem _petItem;
    private readonly ToolStripMenuItem _sleepItem;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly ToolStripMenuItem _hideOnFullScreenItem;

    private Icon? _currentIcon;
    private bool _asleep;
    private bool _suppressCheckEvents;
    private bool _disposed;

    public TrayController()
    {
        _feedItem = new ToolStripMenuItem("ごはんをあげる(&F)");
        _feedItem.Click += (_, _) => FeedRequested?.Invoke(this, EventArgs.Empty);

        _petItem = new ToolStripMenuItem("なでる(&P)");
        _petItem.Click += (_, _) => PetRequested?.Invoke(this, EventArgs.Empty);

        _sleepItem = new ToolStripMenuItem("寝かせる(&S)");
        _sleepItem.Click += (_, _) => SleepToggleRequested?.Invoke(this, EventArgs.Empty);

        var statusItem = new ToolStripMenuItem("ようすを見る(&I)");
        statusItem.Click += (_, _) => StatusRequested?.Invoke(this, EventArgs.Empty);

        _autoStartItem = new ToolStripMenuItem("Windows 起動時に開始") { CheckOnClick = true };
        _autoStartItem.CheckedChanged += (_, _) =>
        {
            if (!_suppressCheckEvents)
            {
                AutoStartChanged?.Invoke(this, _autoStartItem.Checked);
            }
        };

        _hideOnFullScreenItem = new ToolStripMenuItem("全画面アプリ中は隠す") { CheckOnClick = true };
        _hideOnFullScreenItem.CheckedChanged += (_, _) =>
        {
            if (!_suppressCheckEvents)
            {
                HideOnFullScreenChanged?.Invoke(this, _hideOnFullScreenItem.Checked);
            }
        };

        var exitItem = new ToolStripMenuItem("終了(&X)");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            _feedItem,
            _petItem,
            _sleepItem,
            new ToolStripSeparator(),
            statusItem,
            new ToolStripSeparator(),
            _autoStartItem,
            _hideOnFullScreenItem,
            new ToolStripSeparator(),
            exitItem,
        });

        _currentIcon = TrayIconFactory.Create(asleep: false);
        _notifyIcon = new NotifyIcon
        {
            Icon = _currentIcon,
            Text = "メンダコ",
            Visible = true,
            ContextMenuStrip = menu,
        };

        _notifyIcon.MouseClick += OnMouseClick;
        _notifyIcon.BalloonTipClicked += (_, _) => StatusRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? FeedRequested;

    public event EventHandler? PetRequested;

    public event EventHandler? SleepToggleRequested;

    public event EventHandler? StatusRequested;

    public event EventHandler? ExitRequested;

    public event EventHandler<bool>? AutoStartChanged;

    public event EventHandler<bool>? HideOnFullScreenChanged;

    /// <summary>チェック項目の初期値を、イベントを発火させずに設定する。</summary>
    public void InitializeToggles(bool autoStart, bool hideOnFullScreen)
    {
        _suppressCheckEvents = true;
        try
        {
            _autoStartItem.Checked = autoStart;
            _hideOnFullScreenItem.Checked = hideOnFullScreen;
        }
        finally
        {
            _suppressCheckEvents = false;
        }
    }

    /// <summary>ツールチップとアイコンを最新の状態に合わせる。</summary>
    public void UpdateState(MendakoState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _sleepItem.Text = state.IsAsleep ? "起こす(&S)" : "寝かせる(&S)";
        _feedItem.Enabled = !state.IsAsleep;
        _petItem.Enabled = !state.IsAsleep;

        if (_asleep != state.IsAsleep)
        {
            _asleep = state.IsAsleep;
            SwapIcon(TrayIconFactory.Create(asleep: _asleep));
        }

        // NotifyIcon.Text は 63 文字までなので、詳細はバルーン側に回す
        _notifyIcon.Text = Truncate(
            $"{state.Name} ({GrowthStages.DisplayName(state.Stage)}) / {Moods.DisplayName(state.Mood)}",
            63);
    }

    public void ShowBalloon(string title, string text)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.BalloonTipIcon = ToolTipIcon.None;
        _notifyIcon.ShowBalloonTip(5000);
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            StatusRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SwapIcon(Icon icon)
    {
        var previous = _currentIcon;
        _currentIcon = icon;
        _notifyIcon.Icon = icon;
        previous?.Dispose();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Visible を落としてから破棄しないとアイコンがトレイに residue として残る
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _currentIcon?.Dispose();
    }
}

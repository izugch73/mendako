using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Mendako.App.Services;
using Mendako.Core.Model;
using Mendako.Core.Simulation;
using Mendako.Platform;

namespace Mendako.App.Views;

/// <summary>
/// もちもの。持っているアイテムを選んで売り、かいがらに換える。
/// 状態は <see cref="MendakoSession"/> が持っていて、ここは映して操作を渡すだけ。
/// </summary>
public partial class InventoryWindow : Window
{
    private readonly MendakoSession _session;
    private readonly Func<ScatterSpot?> _chooseSpot;

    /// <summary>いま一覧に出している持ち物。同じ並びなら一覧を作り直さない。</summary>
    private IReadOnlyList<ItemId> _shownItems = Array.Empty<ItemId>();

    /// <param name="chooseSpot">アイテムを置く場所を決める。メンダコの居場所を知っている本体の窓が答える。</param>
    public InventoryWindow(MendakoSession session, Func<ScatterSpot?> chooseSpot)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _chooseSpot = chooseSpot ?? throw new ArgumentNullException(nameof(chooseSpot));

        InitializeComponent();

        _session.StateChanged += OnStateChanged;
        Refresh(_session.State);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(new WindowInteropHelper(this).Handle);
    }

    protected override void OnClosed(EventArgs e)
    {
        // 閉じた窓を session が握ったままにすると、状態が変わるたびに閉じた窓を更新しに行く
        _session.StateChanged -= OnStateChanged;
        base.OnClosed(e);
    }

    private void OnStateChanged(object? sender, MendakoState state) => Refresh(state);

    // IsCancel は ShowDialog で開いた窓でしか窓を閉じない。Show で開くこの窓では Esc もここで閉じる
    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void Refresh(MendakoState state)
    {
        ShellsText.Text = state.Shells.ToString();

        var max = _session.MaxInventoryItems;
        CountText.Text = state.Items.Count >= max
            ? $"{state.Items.Count} / {max} こ (いっぱい)"
            : $"{state.Items.Count} / {max} こ";

        if (state.ScatteredItems.Count > 0)
        {
            CountText.Text += $"　タスクバーに {state.ScatteredItems.Count} / {_session.MaxScatteredItems} こ";
        }

        // 状態は 1 分ごとに変わるが、持ち物はほとんど変わらない。
        // そのたびに作り直すと、選んでいる行とスクロール位置が失われる
        if (!state.Items.SequenceEqual(_shownItems))
        {
            _shownItems = state.Items.ToArray();
            RebuildRows(state.Items);
        }

        if (ItemList.Items.Count == 0)
        {
            EmptyText.Text = state.Stage >= GrowthStage.Elder
                ? "まだ なにも ひろってきていないよ\nぬしが さんぽの ついでに ひろってくるよ"
                : "ぬしになると ひろってくるよ";
            EmptyText.Visibility = Visibility.Visible;
        }
        else
        {
            EmptyText.Visibility = Visibility.Collapsed;
        }
    }

    private void RebuildRows(IReadOnlyList<ItemId> items)
    {
        var selectedId = (ItemList.SelectedItem as ItemRow)?.Id;
        var selectedIndex = ItemList.SelectedIndex;

        var rows = items
            .GroupBy(id => id)
            .Select(group =>
            {
                var definition = ItemCatalog.Get(group.Key);
                return new ItemRow(group.Key, definition.DisplayName, group.Count(), definition.SellPrice, ItemIcons.Get(group.Key));
            })
            .OrderBy(row => IndexInCatalog(row.Id))
            .ToList();

        ItemList.ItemsSource = rows;

        // 売った行が無くなったら、同じ位置の次の行を選ぶ。続けて売れるように
        var next = rows.FindIndex(row => row.Id == selectedId);
        if (next < 0 && selectedIndex >= 0 && rows.Count > 0)
        {
            next = Math.Min(selectedIndex, rows.Count - 1);
        }

        ItemList.SelectedIndex = next;
    }

    private static int IndexInCatalog(ItemId id)
    {
        for (var i = 0; i < ItemCatalog.All.Count; i++)
        {
            if (ItemCatalog.All[i].Id == id)
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        SellButton.IsEnabled = ItemList.SelectedItem is ItemRow;

    private void OnSellClick(object sender, RoutedEventArgs e)
    {
        if (ItemList.SelectedItem is not ItemRow row)
        {
            return;
        }

        var outcome = _session.Sell(row.Id);
        MessageText.Text = outcome.Result switch
        {
            SellResult.Sold => $"{row.Name} を うって {outcome.ShellsGained} かいがら もらった",
            _ => $"{row.Name} は もう もっていなかった",
        };
    }

    private void OnScatterClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ItemRow row })
        {
            return;
        }

        if (_session.State.ScatteredItems.Count >= _session.MaxScatteredItems)
        {
            MessageText.Text = $"タスクバーには {_session.MaxScatteredItems} こまで。おいてあるものを クリックすると もどせるよ";
            return;
        }

        if (_chooseSpot() is not { } spot)
        {
            MessageText.Text = "いまは タスクバーに おけなかった";
            return;
        }

        var outcome = _session.Scatter(row.Id, spot.Position, spot.MonitorId);
        MessageText.Text = outcome.Result switch
        {
            ScatterResult.Placed => $"{row.Name} を タスクバーに おいた",
            ScatterResult.TooMany => $"タスクバーには {_session.MaxScatteredItems} こまで。おいてあるものを クリックすると もどせるよ",
            _ => $"{row.Name} は もう もっていなかった",
        };
    }

    /// <summary>一覧の 1 行。同じアイテムは 1 行にまとめ、個数を持つ。</summary>
    private sealed record ItemRow(ItemId Id, string Name, int Count, int Price, ImageSource Icon)
    {
        public string CountText => Count > 1 ? $"×{Count}" : string.Empty;
    }
}

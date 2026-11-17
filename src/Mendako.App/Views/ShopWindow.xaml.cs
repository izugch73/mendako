using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Mendako.App.Services;
using Mendako.Core.Model;
using Mendako.Core.Simulation;
using Mendako.Platform;

namespace Mendako.App.Views;

/// <summary>
/// おみせ。かいがらで飾りを買い、持っている飾りを付け外しする。
/// 付け外しをここに置いたのは、買った飾りを着けた姿をその場で見比べられるため。
/// </summary>
public partial class ShopWindow : Window
{
    private readonly MendakoSession _session;
    private readonly List<AccessoryRow> _rows = new();

    public ShopWindow(MendakoSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));

        InitializeComponent();

        // 安い順に並べる。値段のない飾りは売り物ではないので出さない
        var forSale = Enum.GetValues<Accessory>()
            .Select(accessory => (Accessory: accessory, Price: _session.PriceOf(accessory)))
            .Where(item => item.Price is not null)
            .OrderBy(item => item.Price);

        foreach (var (accessory, price) in forSale)
        {
            var row = new AccessoryRow(accessory, price!.Value) { Margin = new Thickness(0, 0, 0, 6) };
            row.ActionRequested += OnActionRequested;
            _rows.Add(row);
            Rows.Children.Add(row);
        }

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

        foreach (var row in _rows)
        {
            row.Update(state);
        }
    }

    private void OnActionRequested(object? sender, AccessoryRow.AccessoryAction action)
    {
        if (sender is not AccessoryRow row)
        {
            return;
        }

        var name = Accessories.DisplayName(row.Accessory);

        switch (action)
        {
            case AccessoryRow.AccessoryAction.Buy:
                var purchase = _session.Buy(row.Accessory);
                MessageText.Text = purchase.Result switch
                {
                    PurchaseResult.Bought => $"{name} を かった。つけてみよう",
                    PurchaseResult.NotEnoughShells => $"かいがらが あと {purchase.ShellsShort} たりないよ",
                    PurchaseResult.AlreadyOwned => $"{name} は もう もっているよ",
                    _ => $"{name} は いま かえないよ",
                };
                break;

            case AccessoryRow.AccessoryAction.Equip:
                var equip = _session.Equip(row.Accessory);
                MessageText.Text = equip.Result == EquipResult.Equipped ? $"{name} を つけた" : string.Empty;
                break;

            case AccessoryRow.AccessoryAction.Unequip:
                var unequip = _session.Equip(Accessory.None);
                MessageText.Text = unequip.Result == EquipResult.Unequipped ? $"{name} を はずした" : string.Empty;
                break;
        }
    }
}

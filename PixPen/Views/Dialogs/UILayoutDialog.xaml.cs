using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using PixPen.Models;

namespace PixPen.Views.Dialogs;

public partial class UILayoutDialog : Window
{
    // 各列のデータ
    private readonly ObservableCollection<PanelItem> _leftItems   = new();
    private readonly ObservableCollection<PanelItem> _hiddenItems = new();
    private readonly ObservableCollection<PanelItem> _rightItems  = new();

    public UILayoutDialog(AppSettings settings)
    {
        InitializeComponent();

        // 現在の設定を 3 列に振り分ける
        foreach (var p in settings.PanelLayouts.OrderBy(x => x.DockOrder))
        {
            var item = new PanelItem(p.Name);
            switch (p.Side)
            {
                case PanelSide.Left:             _leftItems.Add(item);   break;
                case PanelSide.Right:            _rightItems.Add(item);  break;
                // Hidden / Float はまとめて「非表示」列に表示
                default:                         _hiddenItems.Add(item); break;
            }
        }

        LeftList.ItemsSource   = _leftItems;
        HiddenList.ItemsSource = _hiddenItems;
        RightList.ItemsSource  = _rightItems;
    }

    // ─── 左 ↑↓ ─────────────────────────────────────────────────────────────

    private void OnLeftUp(object sender, RoutedEventArgs e)
        => MoveUp(LeftList, _leftItems);

    private void OnLeftDown(object sender, RoutedEventArgs e)
        => MoveDown(LeftList, _leftItems);

    // ─── 左 → 非表示 ────────────────────────────────────────────────────────

    private void OnLeftToHidden(object sender, RoutedEventArgs e)
        => TransferSelected(LeftList, _leftItems, _hiddenItems, HiddenList);

    // ─── 非表示 → 左 ────────────────────────────────────────────────────────

    private void OnHiddenToLeft(object sender, RoutedEventArgs e)
        => TransferSelected(HiddenList, _hiddenItems, _leftItems, LeftList);

    // ─── 非表示 → 右 ────────────────────────────────────────────────────────

    private void OnHiddenToRight(object sender, RoutedEventArgs e)
        => TransferSelected(HiddenList, _hiddenItems, _rightItems, RightList);

    // ─── 右 → 非表示 ────────────────────────────────────────────────────────

    private void OnRightToHidden(object sender, RoutedEventArgs e)
        => TransferSelected(RightList, _rightItems, _hiddenItems, HiddenList);

    // ─── 右 ↑↓ ─────────────────────────────────────────────────────────────

    private void OnRightUp(object sender, RoutedEventArgs e)
        => MoveUp(RightList, _rightItems);

    private void OnRightDown(object sender, RoutedEventArgs e)
        => MoveDown(RightList, _rightItems);

    // ─── デフォルトに戻す ────────────────────────────────────────────────────

    private void OnReset(object sender, RoutedEventArgs e)
    {
        _leftItems.Clear();
        _hiddenItems.Clear();
        _rightItems.Clear();

        foreach (var d in AppSettings.CreateDefaultLayouts())
            _rightItems.Add(new PanelItem(d.Name));
    }

    // ─── OK ─────────────────────────────────────────────────────────────────

    private void OnOk(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    // ─── 確定したレイアウトを設定に書き戻す ─────────────────────────────────

    /// <summary>OK 後に MainWindow が呼んで設定に反映させる</summary>
    public void ApplyTo(AppSettings settings)
    {
        settings.PanelLayouts.Clear();

        int order = 0;
        foreach (var item in _leftItems)
            settings.PanelLayouts.Add(new PanelLayoutInfo
            {
                Name = item.Name, Side = PanelSide.Left, DockOrder = order++,
                FloatLeft = -1, FloatTop = -1, FloatWidth = 280, FloatHeight = 360
            });

        foreach (var item in _hiddenItems)
            settings.PanelLayouts.Add(new PanelLayoutInfo
            {
                Name = item.Name, Side = PanelSide.Hidden, DockOrder = order++,
                FloatLeft = -1, FloatTop = -1, FloatWidth = 280, FloatHeight = 360
            });

        foreach (var item in _rightItems)
            settings.PanelLayouts.Add(new PanelLayoutInfo
            {
                Name = item.Name, Side = PanelSide.Right, DockOrder = order++,
                FloatLeft = -1, FloatTop = -1, FloatWidth = 280, FloatHeight = 360
            });
    }

    // ─── ヘルパー ────────────────────────────────────────────────────────────

    private static void TransferSelected(
        ListBox fromList,
        ObservableCollection<PanelItem> from,
        ObservableCollection<PanelItem> to,
        ListBox toList)
    {
        if (fromList.SelectedItem is not PanelItem item) return;
        from.Remove(item);
        to.Add(item);
        toList.SelectedItem = item;
    }

    private static void MoveUp(ListBox list, ObservableCollection<PanelItem> col)
    {
        if (list.SelectedItem is not PanelItem item) return;
        int idx = col.IndexOf(item);
        if (idx > 0) col.Move(idx, idx - 1);
    }

    private static void MoveDown(ListBox list, ObservableCollection<PanelItem> col)
    {
        if (list.SelectedItem is not PanelItem item) return;
        int idx = col.IndexOf(item);
        if (idx >= 0 && idx < col.Count - 1) col.Move(idx, idx + 1);
    }
}

/// <summary>ダイアログ内でパネルを表す行データ</summary>
public class PanelItem
{
    public string Name { get; }
    public string DisplayName { get; }

    public PanelItem(string name)
    {
        Name = name;
        DisplayName = name switch
        {
            "Color" => "カラー",
            "Pen"   => "ペン",
            "Layer" => "レイヤー",
            _       => name
        };
    }
}

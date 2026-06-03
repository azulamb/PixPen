using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PixPen.Models;
using PixPen.ViewModels;
using PixPen.Views;
using PixPen.Views.Panels;

namespace PixPen;

public partial class MainWindow : Window
{
    private MainViewModel _vm = null!;

    // ─── パネルインスタンス（1 つのみ生成して使い回す） ──────────────────────
    private ColorPalettePanel _colorPanel = null!;
    private PenPalettePanel   _penPanel   = null!;
    private LayerPanel        _layerPanel = null!;

    // フロート中のウィンドウ（キー = パネル名）
    private readonly Dictionary<string, FloatingPanelWindow> _floatWindows = new();

    // ─── 初期化 ────────────────────────────────────────────────────────────────

    public MainWindow()
    {
        InitializeComponent();

        _vm = new MainViewModel();
        DataContext = _vm;

        // パネルインスタンス作成
        _colorPanel = new ColorPalettePanel();
        _penPanel   = new PenPalettePanel();
        _layerPanel = new LayerPanel();

        // DataContext を ActiveTab に追随させる
        UpdatePanelDataContexts();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.ActiveTab))
            {
                UpdatePanelDataContexts();
                EnsureActiveTabWinTabInitialized();
            }
        };

        _vm.UILayoutRequested           += ShowUILayoutDialog;
        _vm.TabletServiceRebuildRequested += () => _vm.RebuildTabletServices(this);

        // レイアウト適用
        RebuildSidePanels();

        // アイコン設定
        try
        {
            var uri = new Uri("pack://application:,,,/Resources/PixPen.ico");
            Icon = System.Windows.Media.Imaging.BitmapFrame.Create(uri);
        }
        catch { }
    }

    // ─── パネル DataContext 更新 ─────────────────────────────────────────────

    private void UpdatePanelDataContexts()
    {
        _colorPanel.DataContext = _vm.ActiveTab;
        _penPanel.DataContext   = _vm.ActiveTab;
        _layerPanel.DataContext = _vm.ActiveTab;
    }

    // ─── パネルヘルパー ─────────────────────────────────────────────────────

    private FrameworkElement GetPanelControl(string name) => name switch
    {
        "Color" => _colorPanel,
        "Pen"   => _penPanel,
        "Layer" => _layerPanel,
        _ => throw new ArgumentException($"Unknown panel: {name}")
    };

    private static string GetPanelTitle(string name) => name switch
    {
        "Color" => "カラー",
        "Pen"   => "ペン",
        "Layer" => "レイヤー",
        _ => name
    };

    // ─── サイドパネル再構築（両側） ─────────────────────────────────────────

    private void RebuildSidePanels()
    {
        BuildOneSide(LeftSidePanelGrid,  PanelSide.Left);
        BuildOneSide(RightSidePanelGrid, PanelSide.Right);
        UpdateSideColumns();

        // フロートパネルのウィンドウを開く
        foreach (var info in _vm.Settings.PanelLayouts.Where(p => p.Side == PanelSide.Float))
        {
            if (!_floatWindows.ContainsKey(info.Name))
                OpenFloatingWindow(info.Name);
        }
    }

    private void BuildOneSide(Grid sideGrid, PanelSide side)
    {
        sideGrid.Children.Clear();
        sideGrid.RowDefinitions.Clear();

        var panels = _vm.Settings.PanelLayouts
            .Where(p => p.Side == side)
            .OrderBy(p => p.DockOrder)
            .Select(p => p.Name)
            .ToList();

        int gridRow = 0;
        for (int i = 0; i < panels.Count; i++)
        {
            string name = panels[i];
            double heightStar = name == "Color" ? 2.5 : name == "Layer" ? 1.5 : 1.0;

            sideGrid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(heightStar, GridUnitType.Star),
                MinHeight = 80
            });

            var gb = new GroupBox { Padding = new Thickness(0) };
            gb.Header  = CreatePanelHeader(name);
            gb.Content = GetPanelControl(name);
            Grid.SetRow(gb, gridRow++);
            sideGrid.Children.Add(gb);

            if (i < panels.Count - 1)
            {
                sideGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
                var splitter = new GridSplitter
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                    ResizeBehavior = GridResizeBehavior.PreviousAndNext
                };
                Grid.SetRow(splitter, gridRow++);
                sideGrid.Children.Add(splitter);
            }
        }
    }

    // ─── 列幅・スプリッター表示更新 ─────────────────────────────────────────

    private void UpdateSideColumns()
    {
        bool hasLeft  = _vm.Settings.PanelLayouts.Any(p => p.Side == PanelSide.Left);
        bool hasRight = _vm.Settings.PanelLayouts.Any(p => p.Side == PanelSide.Right);

        if (hasLeft)
        {
            double w = Math.Max(160, _vm.Settings.LeftPanelWidth);
            LeftSideCol.Width    = new GridLength(w, GridUnitType.Pixel);
            LeftSideCol.MinWidth = 160;
            LeftSideCol.MaxWidth = 480;
            LeftSplitterCol.Width = new GridLength(4);
            LeftSplitter.Visibility = Visibility.Visible;
        }
        else
        {
            LeftSideCol.Width     = new GridLength(0);
            LeftSplitterCol.Width = new GridLength(0);
            LeftSplitter.Visibility = Visibility.Collapsed;
        }

        if (hasRight)
        {
            double w = Math.Max(160, _vm.Settings.RightPanelWidth);
            RightSideCol.Width    = new GridLength(w, GridUnitType.Pixel);
            RightSideCol.MinWidth = 160;
            RightSideCol.MaxWidth = 480;
            RightSplitterCol.Width = new GridLength(4);
            RightSplitter.Visibility = Visibility.Visible;
        }
        else
        {
            RightSideCol.Width     = new GridLength(0);
            RightSplitterCol.Width = new GridLength(0);
            RightSplitter.Visibility = Visibility.Collapsed;
        }
    }

    // ─── GroupBox ヘッダー（フロートボタン付き） ─────────────────────────────

    private UIElement CreatePanelHeader(string name)
    {
        var dp = new System.Windows.Controls.DockPanel();

        var floatBtn = new Button
        {
            Content = "↗",
            ToolTip = "フロートウィンドウで表示する",
            Width = 20,
            Height = 18,
            Padding = new Thickness(0),
            Margin = new Thickness(2, 0, 2, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = SystemColors.ControlTextBrush,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };
        floatBtn.Click += (_, _) => FloatPanel(name);
        System.Windows.Controls.DockPanel.SetDock(floatBtn, Dock.Right);
        dp.Children.Add(floatBtn);

        var title = new TextBlock
        {
            Text = GetPanelTitle(name),
            VerticalAlignment = VerticalAlignment.Center
        };
        dp.Children.Add(title);

        return dp;
    }

    // ─── フロート ────────────────────────────────────────────────────────────

    private void FloatPanel(string name)
    {
        if (_floatWindows.ContainsKey(name)) return;

        var info = _vm.Settings.GetOrCreate(name);
        info.Side = PanelSide.Float;

        OpenFloatingWindow(name);
        RebuildSidePanels();
        SaveLayout();
    }

    private void OpenFloatingWindow(string name)
    {
        if (_floatWindows.ContainsKey(name)) return;

        var info  = _vm.Settings.GetOrCreate(name);
        var panel = GetPanelControl(name);

        var win = new FloatingPanelWindow(name, GetPanelTitle(name), panel);
        win.Owner = this;

        if (info.FloatLeft >= 0 && info.FloatTop >= 0)
        {
            win.Left = info.FloatLeft;
            win.Top  = info.FloatTop;
        }
        else
        {
            win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        win.Width  = info.FloatWidth;
        win.Height = info.FloatHeight;

        win.DockRequested   += (_, panelName) => AttachPanelToSide(panelName);
        win.LocationChanged += (_, _) => { info.FloatLeft = win.Left; info.FloatTop = win.Top; };
        win.SizeChanged     += (_, _) => { info.FloatWidth = win.ActualWidth; info.FloatHeight = win.ActualHeight; };

        _floatWindows[name] = win;
        win.Show();
    }

    // ─── ドッキング（フロート → 右サイドパネルへ） ──────────────────────────

    private void AttachPanelToSide(string name)
    {
        if (!_floatWindows.TryGetValue(name, out var win)) return;

        var info = _vm.Settings.GetOrCreate(name);
        info.FloatLeft   = win.Left;
        info.FloatTop    = win.Top;
        info.FloatWidth  = win.ActualWidth;
        info.FloatHeight = win.ActualHeight;

        // フロートから戻す先：右パネルに追加（末尾）
        info.Side = PanelSide.Right;
        int maxOrder = _vm.Settings.PanelLayouts
            .Where(p => p.Side == PanelSide.Right)
            .Select(p => p.DockOrder)
            .DefaultIfEmpty(-1)
            .Max();
        info.DockOrder = maxOrder + 1;

        win.DetachPanel();
        _floatWindows.Remove(name);
        win.ForceClose();

        SaveLayout();
        RebuildSidePanels();
    }

    // ─── UI レイアウトダイアログ ─────────────────────────────────────────────

    private void ShowUILayoutDialog()
    {
        var dlg = new Views.Dialogs.UILayoutDialog(_vm.Settings) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        // フロート中パネルのウィンドウを閉じる（設定が変わった場合）
        foreach (var (name, win) in _floatWindows.ToList())
        {
            win.DetachPanel();
            win.ForceClose();
            _floatWindows.Remove(name);
        }

        // ダイアログの結果を設定に反映
        dlg.ApplyTo(_vm.Settings);

        RebuildSidePanels();
        SaveLayout();
    }

    // ─── 設定保存 ───────────────────────────────────────────────────────────

    private void SaveLayout() => _vm.SaveSettings();

    // ─── ドラッグ＆ドロップ ────────────────────────────────────────────────

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Any(f =>
                string.Equals(System.IO.Path.GetExtension(f), ".ppx",
                    StringComparison.OrdinalIgnoreCase)))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                return;
            }
        }
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        if (files == null) return;

        foreach (var file in files.Where(f =>
            string.Equals(System.IO.Path.GetExtension(f), ".ppx",
                StringComparison.OrdinalIgnoreCase)))
        {
            _vm.OpenFile(file);
        }
    }

    // ─── ウィンドウライフサイクル ──────────────────────────────────────────

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_vm.Tabs.Any(t => t.Document.IsModified))
        {
            var dlg = new Views.Dialogs.UnsavedChangesDialog { Owner = this };
            if (dlg.ShowDialog() != true)
            {
                e.Cancel = true;
                return;
            }
        }
        base.OnClosing(e);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        EnsureActiveTabWinTabInitialized();
    }

    /// <summary>
    /// アクティブタブの WinTabTabletService が未初期化であれば Initialize する。
    /// Ctrl+N などで新しいタブが作られるたびに呼ぶことで、
    /// 2 枚目以降のタブでも WinTab 筆圧が機能するようにする。
    /// </summary>
    private void EnsureActiveTabWinTabInitialized()
    {
        if (_vm.ActiveTab?.TabletService is Services.WinTabTabletService wt && !wt.IsAvailable)
            wt.Initialize(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        // フロートウィンドウの位置を保存
        foreach (var (name, win) in _floatWindows)
        {
            var info = _vm.Settings.GetOrCreate(name);
            info.FloatLeft   = win.Left;
            info.FloatTop    = win.Top;
            info.FloatWidth  = win.ActualWidth;
            info.FloatHeight = win.ActualHeight;
        }

        // 現在の列幅を保存
        _vm.Settings.LeftPanelWidth  = LeftSideCol.ActualWidth  > 0 ? LeftSideCol.ActualWidth  : _vm.Settings.LeftPanelWidth;
        _vm.Settings.RightPanelWidth = RightSideCol.ActualWidth > 0 ? RightSideCol.ActualWidth : _vm.Settings.RightPanelWidth;

        SaveLayout();

        // フロートウィンドウを強制終了
        foreach (var win in _floatWindows.Values.ToList())
            win.ForceClose();
        _floatWindows.Clear();

        foreach (var tab in _vm.Tabs)
            tab.TabletService?.Dispose();

        base.OnClosed(e);
    }
}

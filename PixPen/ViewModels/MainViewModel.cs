using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using PixPen.Models;
using PixPen.Services;
using PixPen.Tools;

namespace PixPen.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly AppSettingsService _settingsService = new();
    private readonly FileService _fileService = new();

    public AppSettings Settings { get; private set; }
    public ObservableCollection<CanvasTabViewModel> Tabs { get; } = new();

    private CanvasTabViewModel? _activeTab;
    public CanvasTabViewModel? ActiveTab
    {
        get => _activeTab;
        set => SetField(ref _activeTab, value);
    }

    private bool _isGridVisible;
    public bool IsGridVisible
    {
        get => _isGridVisible;
        set
        {
            SetField(ref _isGridVisible, value);
            if (ActiveTab != null) ActiveTab.IsGridVisible = value;
        }
    }

    public string StatusText => ActiveTab != null
        ? $"ズーム: {ActiveTab.Zoom * 100:F0}%  サイズ: {ActiveTab.Document.Width}×{ActiveTab.Document.Height}" +
          $"  {GetTabletStatus()}"
        : "準備完了";

    private string GetTabletStatus()
    {
        string src = ActiveTab?.LastPressureSource ?? "-";
        double p = ActiveTab?.LastPressure ?? 0;

        // 入力デバイスの種類を判定
        string device = src switch
        {
            var s when s.StartsWith("WinTab")  => "ペンタブ(WinTab)",
            var s when s.StartsWith("WPF")     => "ペンタブ(WPF)",
            var s when s.StartsWith("WMP")     => "ペンタブ(WMP)",
            var s when s.StartsWith("Pointer") => "ペンタブ",
            _                                  => "マウス"
        };

        // WinTab 診断情報
        return $"入力:{device}  筆圧:{p:F2}";
    }

    // ─── ツール選択 ────────────────────────────────────────────────────────

    private ToolKind _selectedTool = ToolKind.Pen;
    public ToolKind SelectedTool
    {
        get => _selectedTool;
        set
        {
            SetField(ref _selectedTool, value);
            if (ActiveTab != null) ActiveTab.ActiveToolKind = value;
        }
    }

    public bool IsToolPen       { get => SelectedTool == ToolKind.Pen;        set { if (value) SelectedTool = ToolKind.Pen; } }
    public bool IsToolEraser    { get => SelectedTool == ToolKind.Eraser;     set { if (value) SelectedTool = ToolKind.Eraser; } }
    public bool IsToolEyedropper{ get => SelectedTool == ToolKind.Eyedropper; set { if (value) SelectedTool = ToolKind.Eyedropper; } }
    public bool IsToolSelection { get => SelectedTool == ToolKind.Selection;  set { if (value) SelectedTool = ToolKind.Selection; } }
    public bool IsToolFill      { get => SelectedTool == ToolKind.Fill;       set { if (value) SelectedTool = ToolKind.Fill; } }

    // ─── コマンド ─────────────────────────────────────────────────────────

    // ファイル
    public RelayCommand NewDocumentCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveAsCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand DocumentSettingsCommand { get; }
    public RelayCommand AppSettingsCommand { get; }
    public RelayCommand TabletSettingsCommand { get; }
    public RelayCommand CloseTabCommand { get; }

    // 編集
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand CutCommand { get; }
    public RelayCommand CopyCommand { get; }
    public RelayCommand PasteCommand { get; }

    // 表示
    public RelayCommand ToggleGridCommand { get; }
    public RelayCommand GridSettingsCommand { get; }

    // ヘルプ
    public RelayCommand AboutCommand { get; }

    // ─── コンストラクタ ─────────────────────────────────────────────────────

    public MainViewModel()
    {
        Settings = _settingsService.Load();

        // ファイル
        NewDocumentCommand = new RelayCommand(NewDocument);
        OpenCommand = new RelayCommand(OpenFile);
        SaveCommand = new RelayCommand(Save, () => ActiveTab != null);
        SaveAsCommand = new RelayCommand(SaveAs, () => ActiveTab != null);
        ExportCommand = new RelayCommand(Export, () => ActiveTab != null);
        DocumentSettingsCommand = new RelayCommand(ShowDocumentSettings, () => ActiveTab != null);
        AppSettingsCommand = new RelayCommand(ShowAppSettings);
        TabletSettingsCommand = new RelayCommand(ShowTabletSettings);
        CloseTabCommand = new RelayCommand(CloseActiveTab, () => ActiveTab != null);

        // 編集
        UndoCommand = new RelayCommand(Undo, () => ActiveTab?.UndoRedo.CanUndo ?? false);
        RedoCommand = new RelayCommand(Redo, () => ActiveTab?.UndoRedo.CanRedo ?? false);
        CutCommand = new RelayCommand(Cut, () => ActiveTab?.Selection.HasSelection ?? false);
        CopyCommand = new RelayCommand(Copy, () => ActiveTab?.Selection.HasSelection ?? false);
        PasteCommand = new RelayCommand(Paste);

        // 表示
        ToggleGridCommand = new RelayCommand(() => IsGridVisible = !IsGridVisible);
        GridSettingsCommand = new RelayCommand(ShowGridSettings, () => ActiveTab != null);

        // ヘルプ
        AboutCommand = new RelayCommand(ShowAbout);

        // 起動時に空のドキュメントを作成
        NewDocument();
    }

    // ─── ドキュメント操作 ──────────────────────────────────────────────────

    private void NewDocument()
    {
        var doc = CreateNewDocument();
        AddTab(doc);
    }

    private Document CreateNewDocument()
    {
        var doc = new Document
        {
            Width = Settings.DefaultWidth,
            Height = Settings.DefaultHeight,
            Dpi = Settings.DefaultDpi,
            Title = "Untitled"
        };
        var layer = new Layer
        {
            Name = "Layer 1",
            Bitmap = FileService.CreateTransparentBitmap(doc.Width, doc.Height, doc.Dpi)
        };
        doc.Layers.Add(layer);

        // デフォルトペンを追加（MinSizeFactor=0: 筆圧0で完全に消え、1で最大サイズ）
        doc.Pens.Add(new PenDefinition { Name = "Pen",       Size = 10, PressureAffectsSize = true, MinSizeFactor = 0.0 });
        doc.Pens.Add(new PenDefinition { Name = "Pen (Large)", Size = 30, PressureAffectsSize = true, MinSizeFactor = 0.0 });

        return doc;
    }

    private void AddTab(Document doc)
    {
        var undoRedo = new UndoRedoService
        {
            MemoryLimitBytes = Settings.UndoMemoryLimitMb * 1024L * 1024L
        };
        var tab = new CanvasTabViewModel(doc, undoRedo, Settings.MaxLayers)
        {
            TabletService = TabletServiceFactory.Create(Settings)
        };
        tab.PropertyChanged += (_, _) => OnPropertyChanged(nameof(StatusText));
        Tabs.Add(tab);
        ActiveTab = tab;
    }

    private void OpenFile()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "PixPen ファイル (*.ppx)|*.ppx",
            Title = "開く"
        };
        if (dlg.ShowDialog() != true) return;
        OpenFile(dlg.FileName);
    }

    /// <summary>指定パスの .ppx を新しいタブで開く（ドラッグ＆ドロップからも使用）</summary>
    public void OpenFile(string path)
    {
        try
        {
            var doc = _fileService.Load(path);
            AddTab(doc);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ファイルを開けませんでした:\n{ex.Message}", "エラー",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Save()
    {
        if (ActiveTab == null) return;
        if (string.IsNullOrEmpty(ActiveTab.Document.FilePath))
        {
            SaveAs();
            return;
        }
        SaveDocument(ActiveTab, ActiveTab.Document.FilePath!);
    }

    private void SaveAs()
    {
        if (ActiveTab == null) return;
        var dlg = new SaveFileDialog
        {
            Filter = "PixPen ファイル (*.ppx)|*.ppx",
            FileName = ActiveTab.Document.Title,
            Title = "名前をつけて保存"
        };
        if (dlg.ShowDialog() != true) return;
        SaveDocument(ActiveTab, dlg.FileName);
    }

    private void SaveDocument(CanvasTabViewModel tab, string path)
    {
        try
        {
            _fileService.Save(tab.Document, path);
            tab.Document.IsModified = false;
            tab.RefreshTitle();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存に失敗しました:\n{ex.Message}", "エラー",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Export()
    {
        if (ActiveTab == null) return;
        ShowDialog<Views.Dialogs.ExportDialog>(ActiveTab);
    }

    private void CloseActiveTab()
    {
        if (ActiveTab == null) return;
        if (ActiveTab.Document.IsModified)
        {
            var result = MessageBox.Show(
                $"「{ActiveTab.Document.Title}」への変更を保存しますか？",
                "確認", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Cancel) return;
            if (result == MessageBoxResult.Yes) Save();
        }
        var tab = ActiveTab;
        Tabs.Remove(tab);
        ActiveTab = Tabs.LastOrDefault();
        if (Tabs.Count == 0) NewDocument();
    }

    // ─── 編集 ──────────────────────────────────────────────────────────────

    private void Undo() => ActiveTab?.Undo();
    private void Redo() => ActiveTab?.Redo();
    private void Cut() => ActiveTab?.SelectionTool.CutSelection(ActiveTab.ActiveLayer!);
    private void Copy() { /* TODO: クリップボード */ }
    private void Paste() { /* TODO: クリップボード貼り付け */ }

    // ─── ダイアログ ────────────────────────────────────────────────────────

    private void ShowDocumentSettings()
        => ShowDialog<Views.Dialogs.DocumentSettingsDialog>(ActiveTab);

    private void ShowAppSettings()
    {
        var dlg = new Views.Dialogs.AppSettingsDialog(Settings)
        {
            Owner = Application.Current.MainWindow
        };
        if (dlg.ShowDialog() == true)
        {
            _settingsService.Save(Settings);
            // メモリ上限を全タブに反映
            foreach (var tab in Tabs)
                tab.UndoRedo.MemoryLimitBytes = Settings.UndoMemoryLimitMb * 1024L * 1024L;
        }
    }

    private void ShowTabletSettings()
    {
        var dlg = new Views.Dialogs.TabletSettingsDialog
        {
            Owner = Application.Current.MainWindow,
            DataContext = Settings
        };
        if (dlg.ShowDialog() == true)
            _settingsService.Save(Settings);
    }

    private void ShowGridSettings()
    {
        if (ActiveTab == null) return;
        var grid = ActiveTab.Document.Grid;

        // キャンセル時に元に戻せるよう事前にバックアップ
        var backup = CloneGrid(grid);

        var dlg = new Views.Dialogs.GridSettingsDialog
        {
            Owner = Application.Current.MainWindow,
            DataContext = grid
        };

        if (dlg.ShowDialog() == true)
        {
            ActiveTab.Document.IsModified = true;
            ActiveTab.RefreshTitle();
        }
        else
        {
            RestoreGrid(grid, backup);
        }

        // OK・キャンセルどちらの場合も Canvas を再描画
        ActiveTab.InvalidateCanvas();
    }

    private static Models.GridSettings CloneGrid(Models.GridSettings src) => new()
    {
        Small  = CloneSingleGrid(src.Small),
        Medium = CloneSingleGrid(src.Medium),
        Large  = CloneSingleGrid(src.Large)
    };

    private static Models.SingleGridSettings CloneSingleGrid(Models.SingleGridSettings s) => new()
    {
        Visible  = s.Visible,
        OffsetX  = s.OffsetX,
        OffsetY  = s.OffsetY,
        SpacingX = s.SpacingX,
        SpacingY = s.SpacingY,
        Color    = s.Color,
        LineType = s.LineType
    };

    private static void RestoreGrid(Models.GridSettings target, Models.GridSettings src)
    {
        CopySingleGrid(target.Small,  src.Small);
        CopySingleGrid(target.Medium, src.Medium);
        CopySingleGrid(target.Large,  src.Large);
    }

    private static void CopySingleGrid(Models.SingleGridSettings t, Models.SingleGridSettings s)
    {
        t.Visible  = s.Visible;
        t.OffsetX  = s.OffsetX;
        t.OffsetY  = s.OffsetY;
        t.SpacingX = s.SpacingX;
        t.SpacingY = s.SpacingY;
        t.Color    = s.Color;
        t.LineType = s.LineType;
    }


    private void ShowAbout()
        => new Views.Dialogs.AboutDialog { Owner = Application.Current.MainWindow }.ShowDialog();

    private static void ShowDialog<TDialog>(object? dataContext = null)
        where TDialog : Window, new()
    {
        var dlg = new TDialog
        {
            Owner = Application.Current.MainWindow,
            DataContext = dataContext
        };
        dlg.ShowDialog();
    }

    // ─── グリッド表示 ──────────────────────────────────────────────────────

    private void ToggleGrid() => IsGridVisible = !IsGridVisible;
}

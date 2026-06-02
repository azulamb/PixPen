using System.Windows;
using System.Windows.Media.Imaging;
using PixPen.Models;
using PixPen.Services;
using PixPen.Tools;

namespace PixPen.ViewModels;

public class CanvasTabViewModel : ViewModelBase
{
    // ─── 公開プロパティ ─────────────────────────────────────────────────────

    public Document Document { get; }
    public UndoRedoService UndoRedo { get; }
    public ITabletService? TabletService { get; set; }
    public int MaxLayers { get; set; } = 100;

    public WriteableBitmap CompositeBitmap { get; private set; }

    private int _activeLayerIndex;
    public int ActiveLayerIndex
    {
        get => _activeLayerIndex;
        set
        {
            value = Math.Clamp(value, 0, Math.Max(0, Document.Layers.Count - 1));
            SetField(ref _activeLayerIndex, value);
        }
    }

    public Layer? ActiveLayer =>
        _activeLayerIndex >= 0 && _activeLayerIndex < Document.Layers.Count
            ? Document.Layers[_activeLayerIndex] : null;

    private ToolKind _activeToolKind = ToolKind.Pen;
    public ToolKind ActiveToolKind
    {
        get => _activeToolKind;
        set => SetField(ref _activeToolKind, value);
    }

    private bool _isGridVisible;
    public bool IsGridVisible
    {
        get => _isGridVisible;
        set { SetField(ref _isGridVisible, value); InvalidateCanvas(); }
    }

    public SelectionMask Selection { get; } = new();

    // 直近の筆圧（ステータスバー表示用）
    private double _lastPressure;
    public double LastPressure
    {
        get => _lastPressure;
        set => SetField(ref _lastPressure, value);
    }

    private string _lastPressureSource = "-";
    public string LastPressureSource
    {
        get => _lastPressureSource;
        set => SetField(ref _lastPressureSource, value);
    }

    // ズーム・パン（DrawingCanvas から読み書き、INPC でステータスバーを更新）
    private double _zoom = 1.0;
    public double Zoom
    {
        get => _zoom;
        set => SetField(ref _zoom, value);
    }
    private double _panX, _panY;
    public double PanX { get => _panX; set => SetField(ref _panX, value); }
    public double PanY { get => _panY; set => SetField(ref _panY, value); }

    // ─── ツール ────────────────────────────────────────────────────────────

    public PenTool PenTool { get; } = new();
    public EraserTool EraserTool { get; } = new();
    public FillTool FillTool { get; } = new();
    public EyedropperTool EyedropperTool { get; }
    public SelectionTool SelectionTool { get; }

    // ─── パネル VM ─────────────────────────────────────────────────────────

    public LayerPanelViewModel LayerPanel { get; }
    public ColorPalettePanelViewModel ColorPanel { get; }
    public PenPalettePanelViewModel PenPanel { get; }

    // DrawingCanvas へ再描画要求を通知
    public event EventHandler? CanvasInvalidated;

    // ─── タイトル ──────────────────────────────────────────────────────────

    public string Title => Document.IsModified
        ? $"* {Document.Title}" : Document.Title;

    // ─── コンストラクタ ─────────────────────────────────────────────────────

    public CanvasTabViewModel(Document document, UndoRedoService undoRedo, int maxLayers)
    {
        Document = document;
        UndoRedo = undoRedo;
        MaxLayers = maxLayers;

        CompositeBitmap = FileService.CreateTransparentBitmap(document.Width, document.Height, document.Dpi);

        EyedropperTool = new EyedropperTool
        {
            CompositeBitmap = CompositeBitmap,
            // ColorPanel は後で初期化されるが、ラムダはクロージャとして遅延評価される
            OnColorPicked = _ => ColorPanel?.RefreshForeground()
        };

        SelectionTool = new SelectionTool
        {
            Mask = Selection,
            OnSelectionChanged = InvalidateCanvas
        };

        LayerPanel = new LayerPanelViewModel(this);
        ColorPanel = new ColorPalettePanelViewModel(document.Palette);
        PenPanel = new PenPalettePanelViewModel(document.Pens);

        // ペンツールに色パレットの筆圧カーブを同期
        UndoRedo.StateChanged += (_, _) => { OnPropertyChanged(nameof(Title)); };

        // 初期合成
        RecompositeAll();
    }

    // ─── 合成 ──────────────────────────────────────────────────────────────

    public void RecompositeAll()
        => RecompositeRegion(new Int32Rect(0, 0, Document.Width, Document.Height));

    public void RecompositeRegion(Int32Rect dirtyRect)
    {
        if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0) return;
        int w = Document.Width, h = Document.Height;
        dirtyRect = ClampRect(dirtyRect, w, h);
        if (dirtyRect.Width <= 0) return;

        CompositeBitmap.Lock();
        unsafe
        {
            byte* dstPtr = (byte*)CompositeBitmap.BackBuffer;
            int dstStride = CompositeBitmap.BackBufferStride;

            // 対象領域をクリア
            for (int y = dirtyRect.Y; y < dirtyRect.Y + dirtyRect.Height; y++)
            {
                var row = new Span<byte>(dstPtr + y * dstStride + dirtyRect.X * 4, dirtyRect.Width * 4);
                row.Clear();
            }

            // レイヤーを下から合成
            foreach (var layer in Enumerable.Reverse(Document.Layers))
            {
                if (!layer.IsVisible || layer.Bitmap == null) continue;
                layer.Bitmap.Lock();
                byte* srcPtr = (byte*)layer.Bitmap.BackBuffer;
                int srcStride = layer.Bitmap.BackBufferStride;
                byte opacityByte = (byte)(layer.Opacity * 255);

                for (int y = dirtyRect.Y; y < dirtyRect.Y + dirtyRect.Height && y < layer.Height; y++)
                for (int x = dirtyRect.X; x < dirtyRect.X + dirtyRect.Width && x < layer.Width; x++)
                {
                    byte* src = srcPtr + y * srcStride + x * 4;
                    byte* dst = dstPtr + y * dstStride + x * 4;
                    FileService.AlphaComposite(src, dst, opacityByte);
                }
                layer.Bitmap.Unlock();
            }
        }
        CompositeBitmap.AddDirtyRect(dirtyRect);
        CompositeBitmap.Unlock();

        CanvasInvalidated?.Invoke(this, EventArgs.Empty);
    }

    // ─── Undo/Redo ─────────────────────────────────────────────────────────

    public void PushStrokeUndo(int layerIndex, Int32Rect region, byte[] before, byte[] after)
    {
        UndoRedo.Push(new StrokeUndoAction
        {
            LayerIndex = layerIndex,
            Region = region,
            BeforePixels = before,
            AfterPixels = after
        });
        Document.IsModified = true;
        OnPropertyChanged(nameof(Title));
    }

    public void Undo()
    {
        UndoRedo.Undo(Document, OnLayerModified);
        LayerPanel.SyncFromDocument();
        Document.IsModified = true;
        OnPropertyChanged(nameof(Title));
    }

    public void Redo()
    {
        UndoRedo.Redo(Document, OnLayerModified);
        LayerPanel.SyncFromDocument();
        Document.IsModified = true;
        OnPropertyChanged(nameof(Title));
    }

    private void OnLayerModified(int layerIndex)
        => RecompositeAll();

    // ─── スポイト ──────────────────────────────────────────────────────────

    public void PickColor(int x, int y)
    {
        var (r, g, b, a) = BitmapHelper.GetPixel(CompositeBitmap, x, y);
        var hex = $"#{a:X2}{r:X2}{g:X2}{b:X2}";
        Document.Palette.ForegroundColor = hex;
        ColorPanel.Foreground = Services.ColorHelper.FromArgbHex(hex);
    }

    // ─── ヘルパー ──────────────────────────────────────────────────────────

    public void InvalidateCanvas() => CanvasInvalidated?.Invoke(this, EventArgs.Empty);

    public void RefreshTitle() => OnPropertyChanged(nameof(Title));

    private static Int32Rect ClampRect(Int32Rect r, int maxW, int maxH)
    {
        int x = Math.Max(0, r.X), y = Math.Max(0, r.Y);
        int x2 = Math.Min(maxW, r.X + r.Width);
        int y2 = Math.Min(maxH, r.Y + r.Height);
        return new Int32Rect(x, y, Math.Max(0, x2 - x), Math.Max(0, y2 - y));
    }
}

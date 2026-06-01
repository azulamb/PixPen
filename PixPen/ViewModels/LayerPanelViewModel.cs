using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;
using PixPen.Models;
using PixPen.Services;

namespace PixPen.ViewModels;

public class LayerPanelViewModel : ViewModelBase
{
    private readonly CanvasTabViewModel _tab;
    private int _selectedIndex;

    public ObservableCollection<LayerViewModel> Layers { get; } = new();

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (SetField(ref _selectedIndex, value))
                _tab.ActiveLayerIndex = value;
        }
    }

    public RelayCommand AddLayerCommand { get; }
    public RelayCommand DeleteLayerCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public RelayCommand DuplicateCommand { get; }

    public LayerPanelViewModel(CanvasTabViewModel tab)
    {
        _tab = tab;

        AddLayerCommand = new RelayCommand(AddLayer,
            () => tab.Document.Layers.Count < tab.MaxLayers);
        DeleteLayerCommand = new RelayCommand(DeleteLayer,
            () => Layers.Count > 1 && SelectedIndex >= 0);
        MoveUpCommand = new RelayCommand(MoveUp,
            () => SelectedIndex > 0);
        MoveDownCommand = new RelayCommand(MoveDown,
            () => SelectedIndex >= 0 && SelectedIndex < Layers.Count - 1);
        DuplicateCommand = new RelayCommand(DuplicateLayer,
            () => SelectedIndex >= 0 && tab.Document.Layers.Count < tab.MaxLayers);

        SyncFromDocument();
    }

    public void SyncFromDocument()
    {
        Layers.Clear();
        foreach (var layer in _tab.Document.Layers)
            Layers.Add(new LayerViewModel(layer, () => _tab.RecompositeAll()));
        _selectedIndex = _tab.ActiveLayerIndex;
        OnPropertyChanged(nameof(SelectedIndex));
    }

    private void AddLayer()
    {
        var doc = _tab.Document;
        var layer = new Layer
        {
            Name = $"Layer {doc.Layers.Count + 1}",
            Bitmap = FileService.CreateTransparentBitmap(doc.Width, doc.Height, doc.Dpi)
        };
        int insertAt = SelectedIndex >= 0 ? SelectedIndex : 0;
        doc.Layers.Insert(insertAt, layer);

        _tab.UndoRedo.Push(new LayerAddUndoAction { LayerIndex = insertAt, Layer = layer });

        SyncFromDocument();
        SelectedIndex = insertAt;
        _tab.RecompositeAll();
    }

    private void DeleteLayer()
    {
        if (SelectedIndex < 0 || Layers.Count <= 1) return;
        int idx = SelectedIndex;
        var layer = _tab.Document.Layers[idx];
        _tab.Document.Layers.RemoveAt(idx);

        _tab.UndoRedo.Push(new LayerRemoveUndoAction { LayerIndex = idx, Layer = layer });

        SyncFromDocument();
        SelectedIndex = Math.Min(idx, Layers.Count - 1);
        _tab.RecompositeAll();
    }

    private void MoveUp()
    {
        int i = SelectedIndex;
        if (i <= 0) return;
        var layers = _tab.Document.Layers;
        (layers[i], layers[i - 1]) = (layers[i - 1], layers[i]);
        SyncFromDocument();
        SelectedIndex = i - 1;
        _tab.RecompositeAll();
    }

    private void MoveDown()
    {
        int i = SelectedIndex;
        var layers = _tab.Document.Layers;
        if (i < 0 || i >= layers.Count - 1) return;
        (layers[i], layers[i + 1]) = (layers[i + 1], layers[i]);
        SyncFromDocument();
        SelectedIndex = i + 1;
        _tab.RecompositeAll();
    }

    private void DuplicateLayer()
    {
        if (SelectedIndex < 0) return;
        var doc = _tab.Document;
        var src = doc.Layers[SelectedIndex];
        var newLayer = new Layer
        {
            Name = src.Name + " コピー",
            IsVisible = src.IsVisible,
            Opacity = src.Opacity,
            Bitmap = CopyBitmap(src.Bitmap!)
        };
        int insertAt = SelectedIndex;
        doc.Layers.Insert(insertAt, newLayer);
        _tab.UndoRedo.Push(new LayerAddUndoAction { LayerIndex = insertAt, Layer = newLayer });
        SyncFromDocument();
        SelectedIndex = insertAt;
        _tab.RecompositeAll();
    }

    private static WriteableBitmap CopyBitmap(WriteableBitmap src)
    {
        var copy = new WriteableBitmap(src.PixelWidth, src.PixelHeight,
            src.DpiX, src.DpiY, src.Format, null);
        var pixels = new byte[src.PixelWidth * src.PixelHeight * 4];
        src.CopyPixels(new Int32Rect(0, 0, src.PixelWidth, src.PixelHeight),
            pixels, src.PixelWidth * 4, 0);
        copy.WritePixels(new Int32Rect(0, 0, src.PixelWidth, src.PixelHeight),
            pixels, src.PixelWidth * 4, 0);
        return copy;
    }
}

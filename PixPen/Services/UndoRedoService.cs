using System.Windows;
using System.Windows.Media.Imaging;
using PixPen.Models;

namespace PixPen.Services;

public abstract class UndoAction
{
    public abstract long MemoryBytes { get; }
    public abstract void Undo(Document doc, Action<int> onLayerModified);
    public abstract void Redo(Document doc, Action<int> onLayerModified);
}

public class StrokeUndoAction : UndoAction
{
    public int LayerIndex { get; init; }
    public Int32Rect Region { get; init; }
    public byte[] BeforePixels { get; init; } = Array.Empty<byte>();
    public byte[] AfterPixels { get; init; } = Array.Empty<byte>();

    public override long MemoryBytes => BeforePixels.Length + AfterPixels.Length;

    public override void Undo(Document doc, Action<int> onLayerModified)
    {
        ApplyPixels(doc.Layers[LayerIndex].Bitmap!, Region, BeforePixels);
        onLayerModified(LayerIndex);
    }

    public override void Redo(Document doc, Action<int> onLayerModified)
    {
        ApplyPixels(doc.Layers[LayerIndex].Bitmap!, Region, AfterPixels);
        onLayerModified(LayerIndex);
    }

    private static void ApplyPixels(WriteableBitmap bmp, Int32Rect rect, byte[] pixels)
        => bmp.WritePixels(rect, pixels, rect.Width * 4, 0);
}

public class LayerAddUndoAction : UndoAction
{
    public int LayerIndex { get; init; }
    public Layer Layer { get; init; } = null!;

    public override long MemoryBytes => (long)Layer.Width * Layer.Height * 4;
    public override void Undo(Document doc, Action<int> onLayerModified) => doc.Layers.RemoveAt(LayerIndex);
    public override void Redo(Document doc, Action<int> onLayerModified) => doc.Layers.Insert(LayerIndex, Layer);
}

public class LayerRemoveUndoAction : UndoAction
{
    public int LayerIndex { get; init; }
    public Layer Layer { get; init; } = null!;

    public override long MemoryBytes => (long)Layer.Width * Layer.Height * 4;
    public override void Undo(Document doc, Action<int> onLayerModified) => doc.Layers.Insert(LayerIndex, Layer);
    public override void Redo(Document doc, Action<int> onLayerModified) => doc.Layers.RemoveAt(LayerIndex);
}

/// <summary>参照レイヤーの変形（移動・拡縮）を Undo/Redo するアクション</summary>
public class RefTransformUndoAction : UndoAction
{
    public Layer Layer { get; init; } = null!;
    public double OldX { get; init; } public double OldY { get; init; }
    public double OldW { get; init; } public double OldH { get; init; }
    public double NewX { get; init; } public double NewY { get; init; }
    public double NewW { get; init; } public double NewH { get; init; }
    public Action? Recomposite { get; init; }

    public override long MemoryBytes => 0;

    public override void Undo(Document doc, Action<int> onLayerModified)
    {
        Layer.RefX = OldX; Layer.RefY = OldY;
        Layer.RefWidth = OldW; Layer.RefHeight = OldH;
        Recomposite?.Invoke();
    }

    public override void Redo(Document doc, Action<int> onLayerModified)
    {
        Layer.RefX = NewX; Layer.RefY = NewY;
        Layer.RefWidth = NewW; Layer.RefHeight = NewH;
        Recomposite?.Invoke();
    }
}

public class UndoRedoService
{
    private readonly LinkedList<UndoAction> _undoStack = new();
    private readonly LinkedList<UndoAction> _redoStack = new();
    private long _totalMemoryBytes = 0;

    public long MemoryLimitBytes { get; set; } = 512L * 1024 * 1024;
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public event EventHandler? StateChanged;

    public void Push(UndoAction action)
    {
        _undoStack.AddLast(action);
        _totalMemoryBytes += action.MemoryBytes;
        ClearRedoStack();
        TrimToLimit();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Undo(Document doc, Action<int> onLayerModified)
    {
        if (!CanUndo) return;
        var action = _undoStack.Last!.Value;
        _undoStack.RemoveLast();
        _totalMemoryBytes -= action.MemoryBytes;
        action.Undo(doc, onLayerModified);
        _redoStack.AddLast(action);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Redo(Document doc, Action<int> onLayerModified)
    {
        if (!CanRedo) return;
        var action = _redoStack.Last!.Value;
        _redoStack.RemoveLast();
        action.Redo(doc, onLayerModified);
        _undoStack.AddLast(action);
        _totalMemoryBytes += action.MemoryBytes;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _undoStack.Clear();
        ClearRedoStack();
        _totalMemoryBytes = 0;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearRedoStack() => _redoStack.Clear();

    private void TrimToLimit()
    {
        while (_totalMemoryBytes > MemoryLimitBytes && _undoStack.Count > 1)
        {
            var oldest = _undoStack.First!.Value;
            _undoStack.RemoveFirst();
            _totalMemoryBytes -= oldest.MemoryBytes;
        }
    }
}

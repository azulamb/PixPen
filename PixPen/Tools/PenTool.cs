using System.Windows;
using System.Windows.Media;
using PixPen.Models;
using PixPen.Services;

namespace PixPen.Tools;

public class PenTool : ITool
{
    public ToolKind Kind => ToolKind.Pen;
    public PenDefinition Definition { get; set; } = new();
    public PressureCurveSettings PressureCurve { get; set; } = new();

    private double _lastX, _lastY;
    private bool _hasLastPoint;
    private Int32Rect _strokeDirty;

    public Int32Rect OnDown(StrokePoint point, Layer layer, ColorPalette palette)
    {
        _hasLastPoint = false;
        _strokeDirty = default;
        return Stamp(point, layer, palette);
    }

    public Int32Rect OnMove(StrokePoint point, Layer layer, ColorPalette palette)
    {
        if (!_hasLastPoint) return Stamp(point, layer, palette);
        var r = DrawLine(_lastX, _lastY, point.X, point.Y, point, layer, palette);
        _strokeDirty = BitmapHelper.UnionRect(_strokeDirty, r);
        return r;
    }

    public Int32Rect OnUp(StrokePoint point, Layer layer, ColorPalette palette)
    {
        _hasLastPoint = false;
        return _strokeDirty;
    }

    private Int32Rect Stamp(StrokePoint point, Layer layer, ColorPalette palette)
    {
        if (layer.Bitmap == null) return default;
        var (r, g, b, a) = GetColor(point, palette);
        int radius = GetRadius(point);
        var dirty = Definition.Shape == PenShape.Round
            ? BitmapHelper.StampRound(layer.Bitmap, (int)point.X, (int)point.Y, radius, r, g, b, a)
            : BitmapHelper.StampSquare(layer.Bitmap, (int)point.X, (int)point.Y, radius, r, g, b, a);
        _lastX = point.X; _lastY = point.Y; _hasLastPoint = true;
        _strokeDirty = BitmapHelper.UnionRect(_strokeDirty, dirty);
        return dirty;
    }

    private Int32Rect DrawLine(double x0, double y0, double x1, double y1, StrokePoint point, Layer layer, ColorPalette palette)
    {
        if (layer.Bitmap == null) return default;
        var (r, g, b, a) = GetColor(point, palette);
        int radius = GetRadius(point);
        var dirty = BitmapHelper.InterpolateStamp(layer.Bitmap, x0, y0, x1, y1, radius, r, g, b, a,
            Definition.Shape == PenShape.Round);
        _lastX = x1; _lastY = y1;
        return dirty;
    }

    private int GetRadius(StrokePoint point)
    {
        double pressure = PressureCurve.Apply(point.Pressure);
        double size = Definition.PressureAffectsSize
            ? Definition.Size * (Definition.MinSizeFactor + (1 - Definition.MinSizeFactor) * pressure)
            : Definition.Size;
        // Math.Ceiling で切り上げ→小さいペンでも圧力変化がより明確に出る
        // 例: size=5 → radius 1〜3, size=20 → radius 1〜10
        return Math.Max(1, (int)Math.Ceiling(size / 2.0));
    }

    private (byte r, byte g, byte b, byte a) GetColor(StrokePoint point, ColorPalette palette)
    {
        var color = ColorHelper.FromArgbHex(palette.ForegroundColor);
        double pressure = PressureCurve.Apply(point.Pressure);
        double opacity = Definition.PressureAffectsOpacity
            ? Definition.Opacity * pressure
            : Definition.Opacity;
        return (color.R, color.G, color.B, (byte)(color.A * opacity));
    }
}

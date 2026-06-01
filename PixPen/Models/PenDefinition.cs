namespace PixPen.Models;

public enum PenShape { Round, Square }

public class PenDefinition
{
    public string Name { get; set; } = "Pen";
    public PenShape Shape { get; set; } = PenShape.Round;
    public double Size { get; set; } = 5.0;
    public double Opacity { get; set; } = 1.0;
    public bool PressureAffectsSize { get; set; } = true;
    public bool PressureAffectsOpacity { get; set; } = false;
    public double MinSizeFactor { get; set; } = 0.0; // 最小サイズ=0（筆圧0で点が消える）
}

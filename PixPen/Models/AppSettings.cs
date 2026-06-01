namespace PixPen.Models;

public enum SidePanelSide { Left, Right }

public class AppSettings
{
    public int UndoMemoryLimitMb { get; set; } = 512;
    public int MaxLayers { get; set; } = 100;
    public bool UseWinTab { get; set; } = false;
    public SidePanelSide SidePanelSide { get; set; } = SidePanelSide.Right;
    public double SidePanelWidth { get; set; } = 240;
    public int DefaultDpi { get; set; } = 96;
    public int DefaultWidth { get; set; } = 1000;
    public int DefaultHeight { get; set; } = 1000;
    public PressureCurveSettings PressureCurve { get; set; } = new();
}

public class PressureCurveSettings
{
    public double MinPressure { get; set; } = 0.0;
    public double MaxPressure { get; set; } = 1.0;
    public double Gamma { get; set; } = 1.0;

    public double Apply(double rawPressure)
    {
        var clamped = Math.Clamp(rawPressure, 0.0, 1.0);
        var normalized = (clamped - MinPressure) / Math.Max(0.001, MaxPressure - MinPressure);
        return Math.Clamp(Math.Pow(Math.Max(0.0, normalized), Gamma), 0.0, 1.0);
    }
}

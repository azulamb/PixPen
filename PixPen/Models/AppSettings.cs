namespace PixPen.Models;

/// <summary>UIテーマモード（System.Windows.ThemeMode と衝突するため AppTheme と命名）</summary>
public enum AppTheme { System, Light, Dark }

/// <summary>パネルの配置場所</summary>
public enum PanelSide { Left, Hidden, Right, Float }

public class PanelLayoutInfo
{
    public string Name { get; set; } = "";
    public PanelSide Side { get; set; } = PanelSide.Right;
    public int DockOrder { get; set; } = 0;
    public double FloatLeft { get; set; } = -1;
    public double FloatTop { get; set; } = -1;
    public double FloatWidth { get; set; } = 280;
    public double FloatHeight { get; set; } = 360;
}

public class AppSettings
{
    public int UndoMemoryLimitMb { get; set; } = 512;
    public int MaxLayers { get; set; } = 100;
    public bool UseWinTab { get; set; } = false;
    public double LeftPanelWidth { get; set; } = 240;
    public double RightPanelWidth { get; set; } = 240;
    public int DefaultDpi { get; set; } = 96;
    public int DefaultWidth { get; set; } = 1000;
    public int DefaultHeight { get; set; } = 1000;
    public PressureCurveSettings PressureCurve { get; set; } = new();
    public AppTheme Theme { get; set; } = AppTheme.System;
    public List<PanelLayoutInfo> PanelLayouts { get; set; } = CreateDefaultLayouts();

    public static List<PanelLayoutInfo> CreateDefaultLayouts() => new()
    {
        new PanelLayoutInfo { Name = "Color", Side = PanelSide.Right, DockOrder = 0 },
        new PanelLayoutInfo { Name = "Pen",   Side = PanelSide.Right, DockOrder = 1 },
        new PanelLayoutInfo { Name = "Layer", Side = PanelSide.Right, DockOrder = 2 }
    };

    public PanelLayoutInfo GetOrCreate(string name)
    {
        var info = PanelLayouts.FirstOrDefault(p => p.Name == name);
        if (info == null)
        {
            info = new PanelLayoutInfo { Name = name, Side = PanelSide.Right, DockOrder = PanelLayouts.Count };
            PanelLayouts.Add(info);
        }
        return info;
    }
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

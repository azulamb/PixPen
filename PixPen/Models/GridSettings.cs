namespace PixPen.Models;

public enum GridLineType { Solid, Dashed }

public class SingleGridSettings
{
    public bool Visible { get; set; } = false;
    public double OffsetX { get; set; } = 0;
    public double OffsetY { get; set; } = 0;
    public double SpacingX { get; set; } = 8;
    public double SpacingY { get; set; } = 8;
    public string Color { get; set; } = "#FF0000FF";
    public GridLineType LineType { get; set; } = GridLineType.Solid;
}

public class GridSettings
{
    // 小グリッドはデフォルトで有効（トグルボタンをONにすれば即座に表示される）
    public SingleGridSettings Small { get; set; } = new()
    {
        Visible = true, SpacingX = 8, SpacingY = 8,
        Color = "#FF808080"
    };
    public SingleGridSettings Medium { get; set; } = new()
    {
        Visible = false, SpacingX = 32, SpacingY = 32,
        Color = "#FF404040"
    };
    public SingleGridSettings Large { get; set; } = new()
    {
        Visible = false, SpacingX = 128, SpacingY = 128,
        Color = "#FF202020"
    };
}

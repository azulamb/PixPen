namespace PixPen.Models;

public static class GridLineTypeValues
{
    public static GridLineType[] All { get; } = (GridLineType[])Enum.GetValues(typeof(GridLineType));
}

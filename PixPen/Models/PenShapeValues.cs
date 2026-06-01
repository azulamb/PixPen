namespace PixPen.Models;

public static class PenShapeValues
{
    public static PenShape[] All { get; } = (PenShape[])Enum.GetValues(typeof(PenShape));
}

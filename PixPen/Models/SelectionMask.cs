using System.Windows;

namespace PixPen.Models;

public class SelectionMask
{
    public bool HasSelection { get; private set; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public Int32Rect Rect => new(X, Y, Width, Height);

    public void SetRect(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = Math.Max(0, width);
        Height = Math.Max(0, height);
        HasSelection = Width > 0 && Height > 0;
    }

    public void Clear()
    {
        HasSelection = false;
        Width = 0;
        Height = 0;
    }

    public bool Contains(int px, int py) =>
        HasSelection && px >= X && px < X + Width && py >= Y && py < Y + Height;
}

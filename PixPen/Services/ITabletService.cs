namespace PixPen.Services;

public record TabletContactArgs(double X, double Y, double Pressure, bool IsEraser);

public interface ITabletService : IDisposable
{
    bool IsAvailable { get; }
    string DisplayName { get; }
    void Initialize(System.Windows.Window window);
}

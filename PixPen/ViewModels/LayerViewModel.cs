using PixPen.Models;

namespace PixPen.ViewModels;

public class LayerViewModel : ViewModelBase
{
    private readonly Layer _layer;
    private readonly Action _onModified;

    public Layer Model => _layer;

    public string Name
    {
        get => _layer.Name;
        set { if (_layer.Name != value) { _layer.Name = value; OnPropertyChanged(); _onModified(); } }
    }

    public bool IsVisible
    {
        get => _layer.IsVisible;
        set { if (_layer.IsVisible != value) { _layer.IsVisible = value; OnPropertyChanged(); _onModified(); } }
    }

    public bool IsLocked
    {
        get => _layer.IsLocked;
        set { if (_layer.IsLocked != value) { _layer.IsLocked = value; OnPropertyChanged(); } }
    }

    /// <summary>参照レイヤーかどうか（読み取り専用）</summary>
    public bool IsReference => _layer.IsReference;

    public double Opacity
    {
        get => _layer.Opacity;
        set
        {
            double clamped = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(_layer.Opacity - clamped) > 0.001)
            {
                _layer.Opacity = clamped;
                OnPropertyChanged();
                _onModified();
            }
        }
    }

    public int OpacityPercent
    {
        get => (int)(_layer.Opacity * 100);
        set => Opacity = value / 100.0;
    }

    public LayerViewModel(Layer layer, Action onModified)
    {
        _layer = layer;
        _onModified = onModified;
    }
}

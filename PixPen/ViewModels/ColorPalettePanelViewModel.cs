using System.Collections.ObjectModel;
using System.Windows.Media;
using PixPen.Models;
using PixPen.Services;

namespace PixPen.ViewModels;

public class ColorPalettePanelViewModel : ViewModelBase
{
    private readonly ColorPalette _palette;

    public ObservableCollection<Color> PaletteColors { get; } = new();

    private Color _foreground;
    public Color Foreground
    {
        get => _foreground;
        set
        {
            if (SetField(ref _foreground, value))
            {
                _palette.ForegroundColor = ColorHelper.ToArgbHex(value);
                UpdateHsv(value);
                // RGB も同期
                OnPropertyChanged(nameof(R));
                OnPropertyChanged(nameof(G));
                OnPropertyChanged(nameof(B));
            }
        }
    }

    // ─── RGB ────────────────────────────────────────────────────────────────
    public byte R
    {
        get => _foreground.R;
        set { if (_foreground.R != value) Foreground = Color.FromArgb(_alpha, value, _foreground.G, _foreground.B); }
    }
    public byte G
    {
        get => _foreground.G;
        set { if (_foreground.G != value) Foreground = Color.FromArgb(_alpha, _foreground.R, value, _foreground.B); }
    }
    public byte B
    {
        get => _foreground.B;
        set { if (_foreground.B != value) Foreground = Color.FromArgb(_alpha, _foreground.R, _foreground.G, value); }
    }

    // ─── HSV ────────────────────────────────────────────────────────────────
    private double _hue, _saturation, _brightness;
    public double Hue { get => _hue; set { SetField(ref _hue, value); UpdateFromHsv(); } }
    public double Saturation { get => _saturation; set { SetField(ref _saturation, value); UpdateFromHsv(); } }
    public double Brightness { get => _brightness; set { SetField(ref _brightness, value); UpdateFromHsv(); } }

    private byte _alpha = 255;
    public byte Alpha
    {
        get => _alpha;
        set
        {
            if (SetField(ref _alpha, value))
            {
                var c = Foreground;
                Foreground = Color.FromArgb(_alpha, c.R, c.G, c.B);
            }
        }
    }

    public RelayCommand AddColorCommand { get; }
    public RelayCommand RemoveColorCommand { get; }
    public RelayCommand<Color> SelectColorCommand { get; }

    public void RefreshForeground()
    {
        _foreground = ColorHelper.FromArgbHex(_palette.ForegroundColor);
        _alpha = _foreground.A;
        OnPropertyChanged(nameof(Foreground));
        OnPropertyChanged(nameof(Alpha));
        OnPropertyChanged(nameof(R));
        OnPropertyChanged(nameof(G));
        OnPropertyChanged(nameof(B));
        UpdateHsv(_foreground);
    }

    private bool _updatingFromHsv;

    public ColorPalettePanelViewModel(ColorPalette palette)
    {
        _palette = palette;

        foreach (var hex in palette.Colors)
            PaletteColors.Add(ColorHelper.FromArgbHex(hex));

        _foreground = ColorHelper.FromArgbHex(palette.ForegroundColor);
        UpdateHsv(_foreground);

        AddColorCommand = new RelayCommand(() =>
        {
            _palette.Colors.Add(ColorHelper.ToArgbHex(Foreground));
            PaletteColors.Add(Foreground);
        }, () => PaletteColors.Count < 256);

        RemoveColorCommand = new RelayCommand(() =>
        {
            if (PaletteColors.Count == 0) return;
            PaletteColors.RemoveAt(PaletteColors.Count - 1);
            _palette.Colors.RemoveAt(_palette.Colors.Count - 1);
        }, () => PaletteColors.Count > 0);

        SelectColorCommand = new RelayCommand<Color>(c => Foreground = c);
    }

    private void UpdateHsv(Color color)
    {
        if (_updatingFromHsv) return;
        var (h, s, v) = ColorHelper.ToHsv(color);
        _hue = h; _saturation = s; _brightness = v; _alpha = color.A;
        OnPropertyChanged(nameof(Hue));
        OnPropertyChanged(nameof(Saturation));
        OnPropertyChanged(nameof(Brightness));
        OnPropertyChanged(nameof(Alpha));
    }

    private void UpdateFromHsv()
    {
        if (_updatingFromHsv) return;
        _updatingFromHsv = true;
        Foreground = ColorHelper.FromHsv(_hue, _saturation, _brightness, _alpha);
        _updatingFromHsv = false;
    }
}

public class RelayCommand<T> : System.Windows.Input.ICommand
{
    private readonly Action<T> _execute;
    public RelayCommand(Action<T> execute) => _execute = execute;
    public bool CanExecute(object? p) => true;
    public void Execute(object? p) { if (p is T t) _execute(t); }
    public event EventHandler? CanExecuteChanged
    {
        add => System.Windows.Input.CommandManager.RequerySuggested += value;
        remove => System.Windows.Input.CommandManager.RequerySuggested -= value;
    }
}

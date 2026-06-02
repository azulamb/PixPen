using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using PixPen.Models;
using PixPen.Services;

namespace PixPen.ViewModels;

public class ColorPalettePanelViewModel : ViewModelBase
{
    private readonly ColorPalette _palette;
    private readonly PaletteService _paletteService = new();

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
                OnPropertyChanged(nameof(R));
                OnPropertyChanged(nameof(G));
                OnPropertyChanged(nameof(B));
                OnPropertyChanged(nameof(OpaqueColor));
            }
        }
    }

    /// <summary>プレビュー左半分用：アルファを 255 に固定した不透明色</summary>
    public Color OpaqueColor => Color.FromArgb(255, _foreground.R, _foreground.G, _foreground.B);

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
                Foreground = Color.FromArgb(_alpha, _foreground.R, _foreground.G, _foreground.B);
        }
    }

    // ─── パレット操作コマンド ────────────────────────────────────────────────
    public RelayCommand AddColorCommand    { get; }
    public RelayCommand RemoveColorCommand { get; }
    public RelayCommand<Color> SelectColorCommand { get; }

    // ─── プリセット管理 ─────────────────────────────────────────────────────
    public ObservableCollection<string> PresetNames { get; } = new();

    private string _presetName = "";
    /// <summary>保存・読み込みに使用するプリセット名（ComboBox + TextBox で編集）</summary>
    public string PresetName
    {
        get => _presetName;
        set => SetField(ref _presetName, value);
    }

    public RelayCommand SavePresetCommand   { get; }
    public RelayCommand LoadPresetCommand   { get; }
    public RelayCommand DeletePresetCommand { get; }

    // ─── コンストラクタ ─────────────────────────────────────────────────────
    public ColorPalettePanelViewModel(ColorPalette palette)
    {
        _palette = palette;

        foreach (var hex in palette.Colors)
            PaletteColors.Add(ColorHelper.FromArgbHex(hex));

        _foreground = ColorHelper.FromArgbHex(palette.ForegroundColor);
        UpdateHsv(_foreground);

        // ── パレット操作 ──
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

        // ── プリセット管理 ──
        SavePresetCommand = new RelayCommand(SavePreset,
            () => !string.IsNullOrWhiteSpace(PresetName));

        LoadPresetCommand = new RelayCommand(LoadPreset,
            () => !string.IsNullOrWhiteSpace(PresetName) && _paletteService.Exists(PresetName));

        DeletePresetCommand = new RelayCommand(DeletePreset,
            () => !string.IsNullOrWhiteSpace(PresetName) && _paletteService.Exists(PresetName));

        RefreshPresetNames();
    }

    // ─── プリセット操作 ─────────────────────────────────────────────────────

    private void SavePreset()
    {
        var name = PresetName.Trim();
        if (string.IsNullOrEmpty(name)) return;

        if (_paletteService.Exists(name))
        {
            if (MessageBox.Show($"'{name}' を上書きしますか？", "確認",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        }

        _paletteService.Save(name, _palette.Colors);
        RefreshPresetNames();
        PresetName = name;
    }

    private void LoadPreset()
    {
        var name = PresetName.Trim();
        if (string.IsNullOrEmpty(name)) return;

        var colors = _paletteService.Load(name);
        if (colors.Count == 0) return;

        _palette.Colors.Clear();
        PaletteColors.Clear();
        foreach (var hex in colors)
        {
            _palette.Colors.Add(hex);
            PaletteColors.Add(ColorHelper.FromArgbHex(hex));
        }
    }

    private void DeletePreset()
    {
        var name = PresetName.Trim();
        if (string.IsNullOrEmpty(name)) return;
        if (MessageBox.Show($"'{name}' を削除しますか？", "確認",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        _paletteService.Delete(name);
        RefreshPresetNames();
        PresetName = PresetNames.Count > 0 ? PresetNames[0] : "";
    }

    private void RefreshPresetNames()
    {
        PresetNames.Clear();
        foreach (var n in _paletteService.GetNames())
            PresetNames.Add(n);
    }

    // ─── 色更新 ─────────────────────────────────────────────────────────────

    public void RefreshForeground()
    {
        _foreground = ColorHelper.FromArgbHex(_palette.ForegroundColor);
        _alpha = _foreground.A;
        OnPropertyChanged(nameof(Foreground));
        OnPropertyChanged(nameof(Alpha));
        OnPropertyChanged(nameof(R));
        OnPropertyChanged(nameof(G));
        OnPropertyChanged(nameof(B));
        OnPropertyChanged(nameof(OpaqueColor));
        UpdateHsv(_foreground);
    }

    private bool _updatingFromHsv;

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

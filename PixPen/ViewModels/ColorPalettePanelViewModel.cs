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
    private readonly AppSettings? _settings;
    private readonly Action? _saveSettings;
    private readonly Action? _onPaletteModified;

    public ObservableCollection<Color> PaletteColors { get; } = new();

    /// <summary>現在選択中のパレットスロット（-1 = 未選択）</summary>
    private int _selectedPaletteIndex = -1;

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
                OnPropertyChanged(nameof(HexCode));

                // 選択中のパレットスロットをリアルタイム更新
                if (_selectedPaletteIndex >= 0 && _selectedPaletteIndex < PaletteColors.Count)
                {
                    PaletteColors[_selectedPaletteIndex]   = value;
                    _palette.Colors[_selectedPaletteIndex] = ColorHelper.ToArgbHex(value);
                    _onPaletteModified?.Invoke();
                }
            }
        }
    }

    /// <summary>プレビュー左半分用：アルファを 255 に固定した不透明色</summary>
    public Color OpaqueColor => Color.FromArgb(255, _foreground.R, _foreground.G, _foreground.B);

    // ─── HEX カラーコード ─────────────────────────────────────────────────────

    public string HexCode
    {
        get => $"{_alpha:X2}{_foreground.R:X2}{_foreground.G:X2}{_foreground.B:X2}";
        set
        {
            var s = (value ?? "").Trim().TrimStart('#').ToUpperInvariant();
            if (!s.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F')) return;
            if (s.Length == 6)
            {
                Foreground = Color.FromArgb(_alpha,
                    Convert.ToByte(s[0..2], 16), Convert.ToByte(s[2..4], 16), Convert.ToByte(s[4..6], 16));
            }
            else if (s.Length == 8)
            {
                byte a = Convert.ToByte(s[0..2], 16);
                _alpha = a;
                Foreground = Color.FromArgb(a,
                    Convert.ToByte(s[2..4], 16), Convert.ToByte(s[4..6], 16), Convert.ToByte(s[6..8], 16));
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
    public double Hue        { get => _hue;        set { SetField(ref _hue, value);        UpdateFromHsv(); } }
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
    public string PresetName
    {
        get => _presetName;
        set
        {
            SetField(ref _presetName, value);
            OnPropertyChanged(nameof(IsCurrentDefault));
        }
    }

    /// <summary>現在選択しているプリセットがデフォルトに設定されているか</summary>
    public bool IsCurrentDefault
    {
        get
        {
            if (_settings == null) return false;
            var name = PresetName.Trim();
            // 基本パレット = DefaultPaletteName が空と同義
            if (name == PaletteService.BasicPaletteName)
                return string.IsNullOrEmpty(_settings.DefaultPaletteName);
            return _settings.DefaultPaletteName == name;
        }
    }

    public RelayCommand SavePresetCommand      { get; }
    public RelayCommand LoadPresetCommand      { get; }
    public RelayCommand DeletePresetCommand    { get; }
    public RelayCommand SetAsDefaultCommand    { get; }

    // ─── コンストラクタ ─────────────────────────────────────────────────────
    public ColorPalettePanelViewModel(
        ColorPalette palette,
        Action? onPaletteModified = null,
        AppSettings? settings = null,
        Action? saveSettings = null)
    {
        _palette          = palette;
        _onPaletteModified = onPaletteModified;
        _settings         = settings;
        _saveSettings     = saveSettings;

        foreach (var hex in palette.Colors)
            PaletteColors.Add(ColorHelper.FromArgbHex(hex));

        _foreground = ColorHelper.FromArgbHex(palette.ForegroundColor);
        UpdateHsv(_foreground);

        // ── パレット操作 ──
        AddColorCommand = new RelayCommand(() =>
        {
            _palette.Colors.Add(ColorHelper.ToArgbHex(Foreground));
            PaletteColors.Add(Foreground);
            _selectedPaletteIndex = -1;
            _onPaletteModified?.Invoke();
        }, () => PaletteColors.Count < 256);

        RemoveColorCommand = new RelayCommand(() =>
        {
            if (PaletteColors.Count == 0) return;
            int lastIdx = PaletteColors.Count - 1;
            if (_selectedPaletteIndex == lastIdx) _selectedPaletteIndex = -1;
            PaletteColors.RemoveAt(lastIdx);
            _palette.Colors.RemoveAt(lastIdx);
            _onPaletteModified?.Invoke();
        }, () => PaletteColors.Count > 0);

        SelectColorCommand = new RelayCommand<Color>(c =>
        {
            _selectedPaletteIndex = PaletteColors.IndexOf(c);
            Foreground = c;
        });

        // ── プリセット管理 ──
        bool IsBasic() => PresetName.Trim() == PaletteService.BasicPaletteName;

        SavePresetCommand = new RelayCommand(SavePreset,
            () => !string.IsNullOrWhiteSpace(PresetName) && !IsBasic());

        LoadPresetCommand = new RelayCommand(LoadPreset,
            () => !string.IsNullOrWhiteSpace(PresetName) && _paletteService.Exists(PresetName.Trim()));

        DeletePresetCommand = new RelayCommand(DeletePreset,
            () => !string.IsNullOrWhiteSpace(PresetName) && !IsBasic()
               && _paletteService.Exists(PresetName.Trim()));

        SetAsDefaultCommand = new RelayCommand(SetAsDefault,
            () => !string.IsNullOrWhiteSpace(PresetName)
               && _settings != null
               && !IsCurrentDefault);

        RefreshPresetNames();

        // 起動時: デフォルトプリセットが設定されていればコンボボックスに表示
        if (_settings != null
            && !string.IsNullOrEmpty(_settings.DefaultPaletteName)
            && PresetNames.Contains(_settings.DefaultPaletteName))
        {
            _presetName = _settings.DefaultPaletteName;
            OnPropertyChanged(nameof(PresetName));
            OnPropertyChanged(nameof(IsCurrentDefault));
        }
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

        _paletteService.Save(name, BuildPreset());
        RefreshPresetNames();
        PresetName = name;
        OnPropertyChanged(nameof(IsCurrentDefault));
    }

    private void LoadPreset()
    {
        var name = PresetName.Trim();
        if (string.IsNullOrEmpty(name)) return;

        var preset = _paletteService.Load(name);
        if (preset.Colors.Count == 0) return;

        _selectedPaletteIndex = -1;
        _palette.Colors.Clear();
        PaletteColors.Clear();
        foreach (var hex in preset.Colors)
        {
            _palette.Colors.Add(hex);
            PaletteColors.Add(ColorHelper.FromArgbHex(hex));
        }
        if (preset.ForegroundColor != null)
            _palette.ForegroundColor = preset.ForegroundColor;
        if (preset.BackgroundColor != null)
            _palette.BackgroundColor = preset.BackgroundColor;

        // 前景色も更新
        _foreground = ColorHelper.FromArgbHex(_palette.ForegroundColor);
        _alpha = _foreground.A;
        OnPropertyChanged(nameof(Foreground));
        OnPropertyChanged(nameof(Alpha));
        OnPropertyChanged(nameof(R));
        OnPropertyChanged(nameof(G));
        OnPropertyChanged(nameof(B));
        OnPropertyChanged(nameof(OpaqueColor));
        OnPropertyChanged(nameof(HexCode));
        UpdateHsv(_foreground);

        _onPaletteModified?.Invoke();
    }

    private void DeletePreset()
    {
        var name = PresetName.Trim();
        if (string.IsNullOrEmpty(name)) return;
        if (MessageBox.Show($"'{name}' を削除しますか？", "確認",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        // デフォルトに設定されていたらリセット
        if (_settings != null && _settings.DefaultPaletteName == name)
        {
            _settings.DefaultPaletteName = "";
            _saveSettings?.Invoke();
            OnPropertyChanged(nameof(IsCurrentDefault));
        }

        _paletteService.Delete(name);
        RefreshPresetNames();
        PresetName = PresetNames.Count > 0 ? PresetNames[0] : PaletteService.BasicPaletteName;
    }

    private void SetAsDefault()
    {
        if (_settings == null) return;
        var name = PresetName.Trim();

        // 基本パレット以外をデフォルトにする場合、
        // Colors[0] を BackgroundColor として含む新形式で上書き保存する。
        // これにより旧形式ファイルでも再起動後にキャンバス背景色が正しく反映される。
        if (name != PaletteService.BasicPaletteName && !string.IsNullOrEmpty(name))
            _paletteService.Save(name, BuildPreset());

        // 基本パレットを選んだ場合はデフォルトをクリア（= 基本パレットに戻す）
        _settings.DefaultPaletteName = name == PaletteService.BasicPaletteName ? "" : name;
        _saveSettings?.Invoke();
        OnPropertyChanged(nameof(IsCurrentDefault));
    }

    /// <summary>
    /// 現在のパレット状態から PalettePreset を生成する。
    /// BackgroundColor にはパレットの先頭色（Colors[0]）を使用する。
    /// これにより新規ドキュメント作成時にそのパレットの背景色でキャンバスが塗りつぶされる。
    /// </summary>
    private PalettePreset BuildPreset() => new()
    {
        Colors          = _palette.Colors.ToList(),
        ForegroundColor = _palette.ForegroundColor,
        BackgroundColor = _palette.Colors.Count > 0 ? _palette.Colors[0] : _palette.BackgroundColor,
    };

    private void RefreshPresetNames()
    {
        var current = PresetName;
        PresetNames.Clear();
        PresetNames.Add(PaletteService.BasicPaletteName);   // 常に先頭に基本パレット
        foreach (var n in _paletteService.GetNames())
            PresetNames.Add(n);
        // 選択を維持（なければ基本パレットに戻す）
        PresetName = PresetNames.Contains(current) ? current : PaletteService.BasicPaletteName;
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
        OnPropertyChanged(nameof(HexCode));
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

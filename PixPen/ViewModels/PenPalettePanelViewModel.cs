using System.Collections.ObjectModel;
using PixPen.Models;

namespace PixPen.ViewModels;

public class PenPalettePanelViewModel : ViewModelBase
{
    private readonly List<PenDefinition> _pens;

    public ObservableCollection<PenDefinition> Pens { get; } = new();

    private PenDefinition? _selectedPen;
    public PenDefinition? SelectedPen
    {
        get => _selectedPen;
        set
        {
            if (SetField(ref _selectedPen, value))
                OnPropertyChanged(nameof(HasSelection));
        }
    }

    public bool HasSelection => SelectedPen != null;

    public RelayCommand AddPenCommand { get; }
    public RelayCommand DeletePenCommand { get; }

    public PenPalettePanelViewModel(List<PenDefinition> pens)
    {
        _pens = pens;
        foreach (var p in pens) Pens.Add(p);

        // 初期選択：最初のペンを選択状態にする
        _selectedPen = Pens.Count > 0 ? Pens[0] : null;

        AddPenCommand = new RelayCommand(() =>
        {
            var pen = new PenDefinition { Name = $"Pen {Pens.Count + 1}" };
            _pens.Add(pen);
            Pens.Add(pen);
            SelectedPen = pen;
        });

        DeletePenCommand = new RelayCommand(() =>
        {
            if (SelectedPen == null || Pens.Count <= 1) return;
            _pens.Remove(SelectedPen);
            Pens.Remove(SelectedPen);
            SelectedPen = Pens.Count > 0 ? Pens[0] : null;
        }, () => SelectedPen != null && Pens.Count > 1);
    }
}

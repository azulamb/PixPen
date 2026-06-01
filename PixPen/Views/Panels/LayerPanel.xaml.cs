using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PixPen.ViewModels;

namespace PixPen.Views.Panels;

public partial class LayerPanel : UserControl
{
    private LayerViewModel? _dragSource;

    public LayerPanel() => InitializeComponent();

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is ListBox lb && lb.SelectedItem is LayerViewModel lvm)
        {
            _dragSource = lvm;
            DragDrop.DoDragDrop(lb, lvm, DragDropEffects.Move);
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
        => e.Effects = e.Data.GetDataPresent(typeof(LayerViewModel))
            ? DragDropEffects.Move : DragDropEffects.None;

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(LayerViewModel))) return;
        if (DataContext is not CanvasTabViewModel tab) return;
        if (e.OriginalSource is not FrameworkElement fe) return;

        var target = FindAncestor<ListBoxItem>(fe)?.DataContext as LayerViewModel;
        if (target == null || _dragSource == null || target == _dragSource) return;

        var layers = tab.Document.Layers;
        var srcIdx = layers.IndexOf(_dragSource.Model);
        var dstIdx = layers.IndexOf(target.Model);
        if (srcIdx < 0 || dstIdx < 0) return;

        layers.RemoveAt(srcIdx);
        layers.Insert(dstIdx, _dragSource.Model);
        tab.LayerPanel.SyncFromDocument();
        tab.LayerPanel.SelectedIndex = dstIdx;
        tab.RecompositeAll();
    }

    private static T? FindAncestor<T>(DependencyObject obj) where T : DependencyObject
    {
        while (obj != null)
        {
            if (obj is T t) return t;
            obj = System.Windows.Media.VisualTreeHelper.GetParent(obj);
        }
        return null;
    }
}

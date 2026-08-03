using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BlockMacro.Models;
using BlockMacro.ViewModels;

namespace BlockMacro;

public partial class MainWindow : Window
{
    private Point _scriptDragStart;
    private MacroBlock? _scriptDragBlock;
    private bool _scriptDragPending;
    private Point _paletteDragStart;
    private bool _paletteDragPending;
    private string? _paletteDragKind;

    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private void ScriptList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm is null || !CanDragEdit())
        {
            return;
        }

        _scriptDragStart = e.GetPosition(null);
        _scriptDragBlock = FindDataContext<MacroBlock>(e.OriginalSource as DependencyObject);
        _scriptDragPending = _scriptDragBlock is not null;
    }

    private void ScriptList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_scriptDragPending
            || _scriptDragBlock is null
            || e.LeftButton != MouseButtonState.Pressed
            || !HasDragMoved(_scriptDragStart, e.GetPosition(null)))
        {
            return;
        }

        _scriptDragPending = false;
        var data = new DataObject(DragFormats.ScriptBlock, _scriptDragBlock);
        DragDrop.DoDragDrop(ScriptList, data, DragDropEffects.Move);
    }

    private void ScriptList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _scriptDragPending = false;
        _scriptDragBlock = null;
    }

    private void ScriptList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;

        if (Vm is null || !CanDragEdit())
        {
            e.Handled = true;
            return;
        }

        var target = FindBlockAtPosition(e.GetPosition(ScriptList));

        if (e.Data.GetDataPresent(DragFormats.PaletteEventKind))
        {
            if (target is ContinueUntilBlock)
            {
                e.Effects = DragDropEffects.Copy;
            }
        }
        else if (e.Data.GetDataPresent(DragFormats.ScriptBlock)
                 && e.Data.GetData(DragFormats.ScriptBlock) is MacroBlock source)
        {
            if (source is EventBlock && target is ContinueUntilBlock && !ReferenceEquals(source, target))
            {
                e.Effects = DragDropEffects.Link;
            }
            else if (!ReferenceEquals(source, target))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        e.Handled = true;
    }

    private void ScriptList_Drop(object sender, DragEventArgs e)
    {
        if (Vm is null || !CanDragEdit())
        {
            return;
        }

        var listPos = e.GetPosition(ScriptList);
        var target = FindBlockAtPosition(listPos);

        if (e.Data.GetDataPresent(DragFormats.PaletteEventKind)
            && e.Data.GetData(DragFormats.PaletteEventKind) is string kind
            && kind == "KeyPressEvent"
            && target is ContinueUntilBlock continueUntilFromPalette)
        {
            Vm.DropPaletteKeyPressEventOnto(continueUntilFromPalette);
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(DragFormats.ScriptBlock)
            && e.Data.GetData(DragFormats.ScriptBlock) is MacroBlock source)
        {
            if (source is EventBlock eventBlock && target is ContinueUntilBlock continueUntil)
            {
                Vm.AssignEventToContinueUntil(continueUntil, eventBlock);
                e.Handled = true;
                return;
            }

            if (target is null)
            {
                Vm.ReorderBlock(source, Vm.Blocks.Count - 1);
            }
            else if (!ReferenceEquals(source, target))
            {
                var insertAfter = IsInLowerHalf(target, listPos);
                Vm.ReorderBlockRelative(source, target, insertAfter);
            }

            e.Handled = true;
        }
    }

    private void PaletteEvent_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!CanDragEdit())
        {
            return;
        }

        _paletteDragStart = e.GetPosition(null);
        _paletteDragPending = true;
        _paletteDragKind = (sender as FrameworkElement)?.Tag as string;
    }

    private void PaletteEvent_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_paletteDragPending
            || _paletteDragKind is null
            || e.LeftButton != MouseButtonState.Pressed
            || !HasDragMoved(_paletteDragStart, e.GetPosition(null))
            || sender is not FrameworkElement element)
        {
            return;
        }

        _paletteDragPending = false;
        var data = new DataObject(DragFormats.PaletteEventKind, _paletteDragKind);
        DragDrop.DoDragDrop(element, data, DragDropEffects.Copy);
    }

    private void PaletteEvent_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _paletteDragPending = false;
        _paletteDragKind = null;
    }

    private bool IsInLowerHalf(MacroBlock target, Point positionInList)
    {
        if (ScriptList.ItemContainerGenerator.ContainerFromItem(target) is not ListBoxItem container)
        {
            return false;
        }

        var posInItem = ScriptList.TranslatePoint(positionInList, container);
        return posInItem.Y > container.ActualHeight / 2;
    }

    private MacroBlock? FindBlockAtPosition(Point positionInList)
    {
        var element = ScriptList.InputHitTest(positionInList) as DependencyObject
                      ?? VisualTreeHelper.HitTest(ScriptList, positionInList)?.VisualHit;
        return FindDataContext<MacroBlock>(element);
    }

    private static T? FindDataContext<T>(DependencyObject? origin) where T : class
    {
        var current = origin;
        while (current is not null)
        {
            if (current is FrameworkElement fe && fe.DataContext is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static bool HasDragMoved(Point start, Point current)
        => Math.Abs(current.X - start.X) > SystemParameters.MinimumHorizontalDragDistance
           || Math.Abs(current.Y - start.Y) > SystemParameters.MinimumVerticalDragDistance;

    private bool CanDragEdit()
        => Vm is { IsRunning: false, IsRecordingLocation: false };
}

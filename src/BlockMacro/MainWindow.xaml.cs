using System.Collections.ObjectModel;
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

    private void BlockItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm is null)
        {
            return;
        }

        var block = FindDataContext<MacroBlock>(e.OriginalSource as DependencyObject);
        if (block is null)
        {
            return;
        }

        Vm.SelectedBlock = block;

        if (!CanDragEdit())
        {
            return;
        }

        _scriptDragStart = e.GetPosition(null);
        _scriptDragBlock = block;
        _scriptDragPending = true;
    }

    private void BlockItem_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_scriptDragPending
            || _scriptDragBlock is null
            || e.LeftButton != MouseButtonState.Pressed
            || !HasDragMoved(_scriptDragStart, e.GetPosition(null))
            || sender is not FrameworkElement element)
        {
            return;
        }

        _scriptDragPending = false;
        var data = new DataObject(DragFormats.ScriptBlock, _scriptDragBlock);
        DragDrop.DoDragDrop(element, data, DragDropEffects.Move);
    }

    private void BlockItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _scriptDragPending = false;
        _scriptDragBlock = null;
    }

    private void ScriptRoot_DragOver(object sender, DragEventArgs e)
        => HandleDragOver(e, Vm?.Blocks, isFlowBody: false, flow: null);

    private void ScriptRoot_Drop(object sender, DragEventArgs e)
        => HandleDrop(e, Vm?.Blocks, isFlowBody: false, flow: null);

    private void FlowHeader_DragOver(object sender, DragEventArgs e)
    {
        var flow = (sender as FrameworkElement)?.DataContext as ContinueUntilBlock
                   ?? FindDataContext<ContinueUntilBlock>(e.OriginalSource as DependencyObject);
        HandleDragOver(e, null, isFlowBody: false, flow: flow);
    }

    private void FlowHeader_Drop(object sender, DragEventArgs e)
    {
        var flow = (sender as FrameworkElement)?.DataContext as ContinueUntilBlock
                   ?? FindDataContext<ContinueUntilBlock>(e.OriginalSource as DependencyObject);
        HandleDrop(e, null, isFlowBody: false, flow: flow);
    }

    private void FlowBody_DragOver(object sender, DragEventArgs e)
    {
        var flow = (sender as FrameworkElement)?.DataContext as ContinueUntilBlock
                   ?? FindDataContext<ContinueUntilBlock>(e.OriginalSource as DependencyObject);
        HandleDragOver(e, flow?.Children, isFlowBody: true, flow: flow);
    }

    private void FlowBody_Drop(object sender, DragEventArgs e)
    {
        var flow = (sender as FrameworkElement)?.DataContext as ContinueUntilBlock
                   ?? FindDataContext<ContinueUntilBlock>(e.OriginalSource as DependencyObject);
        HandleDrop(e, flow?.Children, isFlowBody: true, flow: flow);
    }

    private void HandleDragOver(
        DragEventArgs e,
        ObservableCollection<MacroBlock>? targetOwner,
        bool isFlowBody,
        ContinueUntilBlock? flow)
    {
        e.Effects = DragDropEffects.None;

        if (Vm is null || !CanDragEdit())
        {
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(DragFormats.PaletteEventKind) && flow is not null && !isFlowBody)
        {
            e.Effects = DragDropEffects.Copy;
        }
        else if (e.Data.GetDataPresent(DragFormats.ScriptBlock)
                 && e.Data.GetData(DragFormats.ScriptBlock) is MacroBlock source)
        {
            if (source is EventBlock && flow is not null && !isFlowBody && !ReferenceEquals(source, flow))
            {
                e.Effects = DragDropEffects.Link;
            }
            else if (isFlowBody && targetOwner is not null && !ReferenceEquals(source, flow))
            {
                if (source is ContinueUntilBlock movingFlow && BlockTree.IsOwnedBy(targetOwner, movingFlow))
                {
                    e.Effects = DragDropEffects.None;
                }
                else
                {
                    e.Effects = DragDropEffects.Move;
                }
            }
            else if (!isFlowBody && targetOwner is not null)
            {
                e.Effects = DragDropEffects.Move;
            }
            else if (!isFlowBody && flow is null)
            {
                var under = FindDataContext<MacroBlock>(e.OriginalSource as DependencyObject);
                if (under is not null && !ReferenceEquals(under, source))
                {
                    e.Effects = DragDropEffects.Move;
                }
            }
        }

        e.Handled = true;
    }

    private void HandleDrop(
        DragEventArgs e,
        ObservableCollection<MacroBlock>? targetOwner,
        bool isFlowBody,
        ContinueUntilBlock? flow)
    {
        if (Vm is null || !CanDragEdit())
        {
            return;
        }

        if (e.Data.GetDataPresent(DragFormats.PaletteEventKind)
            && e.Data.GetData(DragFormats.PaletteEventKind) is string kind
            && kind == "KeyPressEvent"
            && flow is not null
            && !isFlowBody)
        {
            Vm.DropPaletteKeyPressEventOnto(flow);
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(DragFormats.ScriptBlock)
            && e.Data.GetData(DragFormats.ScriptBlock) is MacroBlock source)
        {
            if (source is EventBlock eventBlock && flow is not null && !isFlowBody)
            {
                Vm.AssignEventToContinueUntil(flow, eventBlock);
                e.Handled = true;
                return;
            }

            if (isFlowBody && targetOwner is not null)
            {
                var relative = FindDataContext<MacroBlock>(e.OriginalSource as DependencyObject);
                if (relative is not null
                    && !ReferenceEquals(relative, flow)
                    && targetOwner.Contains(relative))
                {
                    var insertAfter = IsLowerHalfOfElement(e.OriginalSource as DependencyObject, e.GetPosition(this));
                    var index = targetOwner.IndexOf(relative) + (insertAfter ? 1 : 0);
                    Vm.MoveBlockInto(source, targetOwner, index);
                }
                else
                {
                    Vm.MoveBlockInto(source, targetOwner, targetOwner.Count);
                }

                e.Handled = true;
                return;
            }

            if (targetOwner is not null && ReferenceEquals(targetOwner, Vm.Blocks))
            {
                var relative = FindDataContext<MacroBlock>(e.OriginalSource as DependencyObject);
                if (relative is ContinueUntilBlock)
                {
                    // Dropped on flow chrome outside body — treat as root reorder relative to flow.
                    var insertAfter = IsLowerHalfOfElement(e.OriginalSource as DependencyObject, e.GetPosition(this));
                    Vm.MoveBlockRelative(source, relative, insertAfter);
                }
                else if (relative is not null && !ReferenceEquals(relative, source))
                {
                    var insertAfter = IsLowerHalfOfElement(e.OriginalSource as DependencyObject, e.GetPosition(this));
                    Vm.MoveBlockRelative(source, relative, insertAfter);
                }
                else
                {
                    Vm.MoveBlockInto(source, Vm.Blocks, Vm.Blocks.Count);
                }

                e.Handled = true;
            }
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

    private static bool IsLowerHalfOfElement(DependencyObject? origin, Point _)
    {
        var current = origin;
        while (current is not null)
        {
            if (current is FrameworkElement fe && fe.DataContext is MacroBlock)
            {
                var pos = Mouse.GetPosition(fe);
                return pos.Y > fe.ActualHeight / 2;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
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

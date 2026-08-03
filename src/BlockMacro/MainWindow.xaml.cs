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
    private readonly DragGhost _ghost = new();
    private readonly InsertionGapController _gaps = new();

    private Point _scriptDragStart;
    private MacroBlock? _scriptDragBlock;
    private bool _scriptDragPending;
    private FrameworkElement? _scriptDragSource;
    private Point _paletteDragStart;
    private bool _paletteDragPending;
    private string? _paletteDragKind;
    private FrameworkElement? _paletteDragSource;

    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) => _ghost.Dispose();
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
        _scriptDragSource = sender as FrameworkElement;
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
        BeginDrag(
            element,
            new DataObject(DragFormats.ScriptBlock, _scriptDragBlock),
            DragDropEffects.Move,
            _scriptDragBlock.DisplayName,
            _scriptDragBlock.Summary);
    }

    private void BlockItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _scriptDragPending = false;
        _scriptDragBlock = null;
        _scriptDragSource = null;
    }

    private void PaletteBlock_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!CanDragEdit())
        {
            return;
        }

        _paletteDragStart = e.GetPosition(null);
        _paletteDragPending = true;
        _paletteDragKind = (sender as FrameworkElement)?.Tag as string;
        _paletteDragSource = sender as FrameworkElement;
    }

    private void PaletteBlock_PreviewMouseMove(object sender, MouseEventArgs e)
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
        var (title, subtitle) = MainViewModel.DescribePaletteKind(_paletteDragKind);
        BeginDrag(
            element,
            new DataObject(DragFormats.PaletteBlockKind, _paletteDragKind),
            DragDropEffects.Copy,
            title,
            subtitle);
    }

    private void PaletteBlock_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _paletteDragPending = false;
        _paletteDragKind = null;
        _paletteDragSource = null;
    }

    private void BeginDrag(
        FrameworkElement source,
        DataObject data,
        DragDropEffects allowed,
        string ghostTitle,
        string ghostSubtitle)
    {
        void OnGiveFeedback(object sender, GiveFeedbackEventArgs args)
        {
            args.UseDefaultCursors = true;
            _ghost.UpdatePosition();
            args.Handled = true;
        }

        source.GiveFeedback += OnGiveFeedback;
        _ghost.Show(ghostTitle, ghostSubtitle);

        try
        {
            DragDrop.DoDragDrop(source, data, allowed);
        }
        finally
        {
            source.GiveFeedback -= OnGiveFeedback;
            EndDragVisuals();
        }
    }

    private void EndDragVisuals()
    {
        _ghost.Hide();
        _gaps.Clear();
        _scriptDragBlock = null;
        _scriptDragSource = null;
        _paletteDragKind = null;
        _paletteDragSource = null;
    }

    private void ScriptRoot_DragOver(object sender, DragEventArgs e)
        => HandleDragOver(e, Vm?.Blocks, ScriptList, isFlowBody: false, flow: null);

    private void ScriptRoot_Drop(object sender, DragEventArgs e)
        => HandleDrop(e, Vm?.Blocks, isFlowBody: false, flow: null);

    private void ScriptRoot_DragLeave(object sender, DragEventArgs e)
    {
        // Only clear when truly leaving the script surface.
        if (sender is FrameworkElement fe)
        {
            var pos = e.GetPosition(fe);
            if (pos.X < 0 || pos.Y < 0 || pos.X > fe.ActualWidth || pos.Y > fe.ActualHeight)
            {
                _gaps.Clear();
            }
        }
    }

    private void FlowHeader_DragOver(object sender, DragEventArgs e)
    {
        var flow = (sender as FrameworkElement)?.DataContext as ContinueUntilBlock
                   ?? FindDataContext<ContinueUntilBlock>(e.OriginalSource as DependencyObject);
        HandleDragOver(e, null, null, isFlowBody: false, flow: flow);
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
        var list = FindItemsControl(e.OriginalSource as DependencyObject)
                   ?? FindItemsControl(sender as DependencyObject);
        HandleDragOver(e, flow?.Children, list, isFlowBody: true, flow: flow);
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
        ItemsControl? list,
        bool isFlowBody,
        ContinueUntilBlock? flow)
    {
        e.Effects = DragDropEffects.None;
        _ghost.UpdatePosition();

        if (Vm is null || !CanDragEdit())
        {
            e.Handled = true;
            return;
        }

        var hasPalette = e.Data.GetDataPresent(DragFormats.PaletteBlockKind);
        var hasScript = e.Data.GetDataPresent(DragFormats.ScriptBlock)
                        && e.Data.GetData(DragFormats.ScriptBlock) is MacroBlock;

        // Assign event onto Continue Until header.
        if (!isFlowBody && flow is not null)
        {
            if (hasPalette && e.Data.GetData(DragFormats.PaletteBlockKind) is string kind && kind == "KeyPressEvent")
            {
                e.Effects = DragDropEffects.Copy;
                _gaps.Clear();
                e.Handled = true;
                return;
            }

            if (hasScript && e.Data.GetData(DragFormats.ScriptBlock) is EventBlock)
            {
                e.Effects = DragDropEffects.Link;
                _gaps.Clear();
                e.Handled = true;
                return;
            }
        }

        if (targetOwner is null)
        {
            // Dropping on flow header as reorder relative to the flow in its parent.
            if (flow is not null
                && BlockTree.TryFindOwner(Vm.Blocks, flow, out var parentOwner, out _)
                && (hasPalette || hasScript))
            {
                var parentList = FindItemsControlForOwner(parentOwner) ?? ScriptList;
                if (hasScript && e.Data.GetData(DragFormats.ScriptBlock) is MacroBlock src
                    && src is ContinueUntilBlock mf
                    && (ReferenceEquals(src, flow) || BlockTree.ContainsBlock(mf, flow)))
                {
                    e.Effects = DragDropEffects.None;
                    _gaps.Clear();
                }
                else if (parentList is not null)
                {
                    e.Effects = hasPalette ? DragDropEffects.Copy : DragDropEffects.Move;
                    var insertIndex = _gaps.ComputeInsertIndex(parentList, e.GetPosition(parentList));
                    _gaps.Show(parentList, parentOwner, insertIndex);
                }
            }

            e.Handled = true;
            return;
        }

        if (hasPalette || hasScript)
        {
            if (hasScript
                && e.Data.GetData(DragFormats.ScriptBlock) is MacroBlock source
                && source is ContinueUntilBlock movingFlow
                && BlockTree.IsOwnedBy(targetOwner, movingFlow))
            {
                e.Effects = DragDropEffects.None;
                _gaps.Clear();
            }
            else
            {
                e.Effects = hasPalette ? DragDropEffects.Copy : DragDropEffects.Move;
                var itemsControl = list ?? FindItemsControl(e.OriginalSource as DependencyObject) ?? ScriptList;
                var insertIndex = _gaps.ComputeInsertIndex(itemsControl, e.GetPosition(itemsControl));
                _gaps.Show(itemsControl, targetOwner, insertIndex);
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

        try
        {
            // Palette / existing event onto Continue Until header → assign.
            if (!isFlowBody && flow is not null)
            {
                if (e.Data.GetDataPresent(DragFormats.PaletteBlockKind)
                    && e.Data.GetData(DragFormats.PaletteBlockKind) is string kind
                    && kind == "KeyPressEvent")
                {
                    Vm.DropPaletteKeyPressEventOnto(flow);
                    e.Handled = true;
                    return;
                }

                if (e.Data.GetDataPresent(DragFormats.ScriptBlock)
                    && e.Data.GetData(DragFormats.ScriptBlock) is EventBlock eventBlock)
                {
                    Vm.AssignEventToContinueUntil(flow, eventBlock);
                    e.Handled = true;
                    return;
                }

                // Other palette/script drops on header → insert around the flow in its parent.
                if (BlockTree.TryFindOwner(Vm.Blocks, flow, out var parentOwner, out _))
                {
                    var parentList = FindItemsControlForOwner(parentOwner) ?? ScriptList;
                    var insertIndex = parentList is not null
                        ? _gaps.ComputeInsertIndex(parentList, e.GetPosition(parentList))
                        : parentOwner.IndexOf(flow);

                    if (e.Data.GetDataPresent(DragFormats.PaletteBlockKind)
                        && e.Data.GetData(DragFormats.PaletteBlockKind) is string paletteKind)
                    {
                        Vm.InsertPaletteBlock(paletteKind, parentOwner, insertIndex);
                        e.Handled = true;
                        return;
                    }

                    if (e.Data.GetDataPresent(DragFormats.ScriptBlock)
                        && e.Data.GetData(DragFormats.ScriptBlock) is MacroBlock source
                        && !ReferenceEquals(source, flow))
                    {
                        Vm.MoveBlockInto(source, parentOwner, insertIndex);
                        e.Handled = true;
                        return;
                    }
                }
            }

            if (targetOwner is null)
            {
                return;
            }

            var list = FindItemsControlForOwner(targetOwner)
                       ?? FindItemsControl(e.OriginalSource as DependencyObject)
                       ?? ScriptList;
            var index = list is not null
                ? _gaps.ComputeInsertIndex(list, e.GetPosition(list))
                : targetOwner.Count;

            if (_gaps.InsertIndex >= 0 && ReferenceEquals(_gaps.TargetOwner, targetOwner))
            {
                index = _gaps.InsertIndex;
            }

            if (e.Data.GetDataPresent(DragFormats.PaletteBlockKind)
                && e.Data.GetData(DragFormats.PaletteBlockKind) is string dropKind)
            {
                Vm.InsertPaletteBlock(dropKind, targetOwner, index);
                e.Handled = true;
                return;
            }

            if (e.Data.GetDataPresent(DragFormats.ScriptBlock)
                && e.Data.GetData(DragFormats.ScriptBlock) is MacroBlock moving)
            {
                Vm.MoveBlockInto(moving, targetOwner, index);
                e.Handled = true;
            }
        }
        finally
        {
            _gaps.Clear();
        }
    }

    private ItemsControl? FindItemsControlForOwner(ObservableCollection<MacroBlock> owner)
    {
        if (ReferenceEquals(owner, Vm?.Blocks))
        {
            return ScriptList;
        }

        return FindItemsControlInTree(ScriptList, owner);
    }

    private static ItemsControl? FindItemsControlInTree(DependencyObject? root, ObservableCollection<MacroBlock> owner)
    {
        if (root is null)
        {
            return null;
        }

        if (root is ItemsControl ic && ReferenceEquals(ic.ItemsSource, owner))
        {
            return ic;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindItemsControlInTree(VisualTreeHelper.GetChild(root, i), owner);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static ItemsControl? FindItemsControl(DependencyObject? start)
    {
        var current = start;
        while (current is not null)
        {
            if (current is ItemsControl ic && ic.ItemsSource is ObservableCollection<MacroBlock>)
            {
                return ic;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
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

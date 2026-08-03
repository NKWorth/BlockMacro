using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MacroBlocks.ViewModels;

namespace MacroBlocks;

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

    private Point _libraryDragStart;
    private bool _libraryDragPending;
    private MacroScript? _libraryDragScript;

    private Point _graphNodeDragStart;
    private FlowGraphNode? _graphDraggingNode;
    private Point _graphNodeOrigin;
    private bool _graphNodeDragMoved;

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

        // Keep keyboard focus in the script panel so Delete can remove the selection
        // without stealing Delete from inspector/name TextBoxes.
        ScriptPanel.Focus();

        if (!CanDragEdit())
        {
            return;
        }

        _scriptDragStart = e.GetPosition(null);
        _scriptDragBlock = block;
        _scriptDragSource = sender as FrameworkElement;
        _scriptDragPending = true;
    }

    private void ScriptPanel_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || IsTextInputFocused())
        {
            return;
        }

        if (Vm?.RemoveSelectedCommand.CanExecute(null) == true)
        {
            Vm.RemoveSelectedCommand.Execute(null);
            e.Handled = true;
            ScriptPanel.Focus();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (IsTextInputFocused() || Vm is null)
        {
            return;
        }

        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        if (!ctrl)
        {
            return;
        }

        if (e.Key == Key.Z && !shift && Vm.UndoCommand.CanExecute(null))
        {
            Vm.UndoCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if ((e.Key == Key.Y || (e.Key == Key.Z && shift)) && Vm.RedoCommand.CanExecute(null))
        {
            Vm.RedoCommand.Execute(null);
            e.Handled = true;
        }
    }

    private static bool IsTextInputFocused()
        => Keyboard.FocusedElement is TextBox or PasswordBox or ComboBox { IsEditable: true };

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
        var kind = (sender as FrameworkElement)?.Tag as string;
        if (kind is not null)
        {
            Vm?.SelectPaletteKind(kind);
        }

        if (!CanDragEdit())
        {
            return;
        }

        _paletteDragStart = e.GetPosition(null);
        _paletteDragPending = true;
        _paletteDragKind = kind;
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
        var title = Vm?.SelectedBlock?.DisplayName;
        var subtitle = Vm?.SelectedBlock?.Summary;
        if (title is null || Vm?.SelectedPaletteKind != _paletteDragKind)
        {
            (title, subtitle) = MainViewModel.DescribePaletteKind(_paletteDragKind);
        }

        BeginDrag(
            element,
            new DataObject(DragFormats.PaletteBlockKind, _paletteDragKind),
            DragDropEffects.Copy,
            title,
            subtitle ?? string.Empty);
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

    private void FlowHeader_DragOver(object sender, DragEventArgs e)
    {
        var flow = (sender as FrameworkElement)?.DataContext as ContinueUntilBlock
                   ?? FindDataContext<ContinueUntilBlock>(e.OriginalSource as DependencyObject);
        // Header is for reordering the flow in its parent — not for event assignment.
        HandleDragOver(e, null, null, isFlowBody: false, flow: flow, isEventSlot: false);
    }

    private void FlowHeader_Drop(object sender, DragEventArgs e)
    {
        var flow = (sender as FrameworkElement)?.DataContext as ContinueUntilBlock
                   ?? FindDataContext<ContinueUntilBlock>(e.OriginalSource as DependencyObject);
        HandleDrop(e, null, isFlowBody: false, flow: flow, isEventSlot: false);
    }

    private void FlowEventSlot_DragOver(object sender, DragEventArgs e)
    {
        var flow = (sender as FrameworkElement)?.DataContext as ContinueUntilBlock
                   ?? FindDataContext<ContinueUntilBlock>(e.OriginalSource as DependencyObject);
        HandleDragOver(e, null, null, isFlowBody: false, flow: flow, isEventSlot: true);
    }

    private void FlowEventSlot_Drop(object sender, DragEventArgs e)
    {
        var flow = (sender as FrameworkElement)?.DataContext as ContinueUntilBlock
                   ?? FindDataContext<ContinueUntilBlock>(e.OriginalSource as DependencyObject);
        HandleDrop(e, null, isFlowBody: false, flow: flow, isEventSlot: true);
    }

    private void FlowBody_DragOver(object sender, DragEventArgs e)
    {
        var flow = (sender as FrameworkElement)?.DataContext as ContinueUntilBlock
                   ?? FindDataContext<ContinueUntilBlock>(e.OriginalSource as DependencyObject);
        var list = FindItemsControl(e.OriginalSource as DependencyObject)
                   ?? FindItemsControl(sender as DependencyObject);
        HandleDragOver(e, flow?.Children, list, isFlowBody: true, flow: flow, isEventSlot: false);
    }

    private void FlowBody_Drop(object sender, DragEventArgs e)
    {
        var flow = (sender as FrameworkElement)?.DataContext as ContinueUntilBlock
                   ?? FindDataContext<ContinueUntilBlock>(e.OriginalSource as DependencyObject);
        HandleDrop(e, flow?.Children, isFlowBody: true, flow: flow, isEventSlot: false);
    }

    private void ScriptRoot_DragOver(object sender, DragEventArgs e)
        => HandleDragOver(e, Vm?.Blocks, ScriptList, isFlowBody: false, flow: null, isEventSlot: false);

    private void ScriptRoot_Drop(object sender, DragEventArgs e)
        => HandleDrop(e, Vm?.Blocks, isFlowBody: false, flow: null, isEventSlot: false);

    private void ScriptRoot_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            var pos = e.GetPosition(fe);
            if (pos.X < 0 || pos.Y < 0 || pos.X > fe.ActualWidth || pos.Y > fe.ActualHeight)
            {
                _gaps.Clear();
            }
        }
    }

    private const double FlowEdgePixels = 16;

    private void HandleDragOver(
        DragEventArgs e,
        ObservableCollection<MacroBlock>? targetOwner,
        ItemsControl? list,
        bool isFlowBody,
        ContinueUntilBlock? flow,
        bool isEventSlot)
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
        if (!hasPalette && !hasScript)
        {
            e.Handled = true;
            return;
        }

        var isEventPayload = (hasPalette
                              && e.Data.GetData(DragFormats.PaletteBlockKind) is string ek
                              && ek == "KeyPressEvent")
                             || (hasScript && e.Data.GetData(DragFormats.ScriptBlock) is EventBlock);

        // Event slot: events only; non-events over interior go to the body.
        if (isEventSlot && flow is not null)
        {
            if (isEventPayload)
            {
                e.Effects = hasPalette ? DragDropEffects.Copy : DragDropEffects.Move;
                _gaps.Clear();
            }
            else if (TryGetFlowChrome(e, flow, out var chrome)
                     && IsNearFlowVerticalEdge(chrome, e.GetPosition(chrome)))
            {
                ShowParentReorder(e, flow, hasPalette);
            }
            else if (!isEventPayload)
            {
                ShowBodyTarget(e, flow, hasPalette);
            }

            e.Handled = true;
            return;
        }

        // Body: reject events; highlight empty body or nudge children.
        if (isFlowBody && flow is not null)
        {
            if (isEventPayload)
            {
                e.Effects = DragDropEffects.None;
                _gaps.Clear();
                e.Handled = true;
                return;
            }

            if (TryGetFlowChrome(e, flow, out var chrome)
                && IsNearFlowVerticalEdge(chrome, e.GetPosition(chrome)))
            {
                ShowParentReorder(e, flow, hasPalette);
            }
            else
            {
                ShowBodyTarget(e, flow, hasPalette, list);
            }

            e.Handled = true;
            return;
        }

        // Header / chrome without body owner: edges reorder in parent; interior nests in body.
        if (targetOwner is null && flow is not null)
        {
            if (isEventPayload)
            {
                e.Effects = DragDropEffects.None;
                _gaps.Clear();
                e.Handled = true;
                return;
            }

            if (TryGetFlowChrome(e, flow, out var chrome)
                && IsNearFlowVerticalEdge(chrome, e.GetPosition(chrome)))
            {
                ShowParentReorder(e, flow, hasPalette);
            }
            else
            {
                ShowBodyTarget(e, flow, hasPalette);
            }

            e.Handled = true;
            return;
        }

        // Script root (or other collection): if over a flow interior, nest; else gap-nudge.
        if (targetOwner is not null && !isFlowBody)
        {
            var underFlow = FindDataContext<ContinueUntilBlock>(e.OriginalSource as DependencyObject);
            if (underFlow is not null
                && !isEventPayload
                && TryGetFlowChrome(e, underFlow, out var underChrome)
                && !IsNearFlowVerticalEdge(underChrome, e.GetPosition(underChrome)))
            {
                ShowBodyTarget(e, underFlow, hasPalette);
                e.Handled = true;
                return;
            }

            if (hasScript
                && e.Data.GetData(DragFormats.ScriptBlock) is MacroBlock source
                && source is ContinueUntilBlock movingFlow
                && BlockTree.IsOwnedBy(targetOwner, movingFlow))
            {
                e.Effects = DragDropEffects.None;
                _gaps.Clear();
            }
            else if (isEventPayload && underFlow is not null)
            {
                // Prefer event slot when over a flow; do not insert events into root via flow hover.
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
        ContinueUntilBlock? flow,
        bool isEventSlot)
    {
        if (Vm is null || !CanDragEdit())
        {
            return;
        }

        try
        {
            var hasPalette = e.Data.GetDataPresent(DragFormats.PaletteBlockKind);
            var hasScript = e.Data.GetDataPresent(DragFormats.ScriptBlock)
                            && e.Data.GetData(DragFormats.ScriptBlock) is MacroBlock;
            var isEventPayload = (hasPalette
                                  && e.Data.GetData(DragFormats.PaletteBlockKind) is string ek
                                  && ek == "KeyPressEvent")
                                 || (hasScript && e.Data.GetData(DragFormats.ScriptBlock) is EventBlock);

            if (isEventSlot && flow is not null && isEventPayload)
            {
                if (hasPalette)
                {
                    Vm.DropPaletteKeyPressEventOnto(flow);
                }
                else if (e.Data.GetData(DragFormats.ScriptBlock) is EventBlock eventBlock)
                {
                    Vm.PlaceEventInSlot(flow, eventBlock);
                }

                e.Handled = true;
                return;
            }

            // Resolve effective target: body interior vs parent edge.
            ContinueUntilBlock? nestFlow = flow
                ?? FindDataContext<ContinueUntilBlock>(e.OriginalSource as DependencyObject);
            var nestInBody = false;
            var reorderInParent = false;

            if (nestFlow is not null && !isEventPayload)
            {
                if (TryGetFlowChrome(e, nestFlow, out var chrome))
                {
                    if (IsNearFlowVerticalEdge(chrome, e.GetPosition(chrome)))
                    {
                        reorderInParent = true;
                    }
                    else if (isFlowBody || isEventSlot || targetOwner is null
                             || ReferenceEquals(targetOwner, Vm.Blocks)
                             || ReferenceEquals(_gaps.TargetOwner, nestFlow.Children))
                    {
                        nestInBody = true;
                    }
                }
                else if (isFlowBody || ReferenceEquals(_gaps.TargetOwner, nestFlow.Children))
                {
                    nestInBody = true;
                }
            }

            if (nestInBody && nestFlow is not null)
            {
                if (isEventPayload)
                {
                    e.Handled = true;
                    return;
                }

                DropIntoOwner(e, nestFlow.Children, index: _gaps.IsEmptyBodyTarget ? 0 : -1);
                e.Handled = true;
                return;
            }

            if (reorderInParent && nestFlow is not null)
            {
                if (BlockTree.TryFindOwner(Vm.Blocks, nestFlow, out var parentOwner, out var flowIndex)
                    && TryGetFlowChrome(e, nestFlow, out var chrome))
                {
                    var pos = e.GetPosition(chrome);
                    var insertIndex = pos.Y < chrome.ActualHeight / 2.0 ? flowIndex : flowIndex + 1;
                    DropIntoOwner(e, parentOwner, insertIndex);
                }

                e.Handled = true;
                return;
            }

            if (isFlowBody && isEventPayload)
            {
                e.Handled = true;
                return;
            }

            if (targetOwner is null)
            {
                return;
            }

            DropIntoOwner(e, targetOwner, index: -1);
            e.Handled = true;
        }
        finally
        {
            _gaps.Clear();
        }
    }

    private void DropIntoOwner(DragEventArgs e, ObservableCollection<MacroBlock> owner, int index)
    {
        if (Vm is null)
        {
            return;
        }

        if (index < 0)
        {
            var list = FindItemsControlForOwner(owner)
                       ?? FindItemsControl(e.OriginalSource as DependencyObject)
                       ?? ScriptList;
            index = list is not null
                ? _gaps.ComputeInsertIndex(list, e.GetPosition(list))
                : owner.Count;

            if (_gaps.InsertIndex >= 0 && ReferenceEquals(_gaps.TargetOwner, owner))
            {
                index = _gaps.InsertIndex;
            }
        }

        if (e.Data.GetDataPresent(DragFormats.PaletteBlockKind)
            && e.Data.GetData(DragFormats.PaletteBlockKind) is string dropKind)
        {
            Vm.InsertPaletteBlock(dropKind, owner, index);
            return;
        }

        if (e.Data.GetDataPresent(DragFormats.ScriptBlock)
            && e.Data.GetData(DragFormats.ScriptBlock) is MacroBlock moving)
        {
            if (moving is EventBlock && IsEventBodyCollection(owner))
            {
                return;
            }

            Vm.MoveBlockInto(moving, owner, index);
        }
    }

    private bool IsEventBodyCollection(ObservableCollection<MacroBlock> owner)
    {
        if (Vm is null)
        {
            return false;
        }

        foreach (var flow in BlockTree.Enumerate(Vm.Blocks).OfType<ContinueUntilBlock>())
        {
            if (ReferenceEquals(flow.Children, owner))
            {
                return true;
            }
        }

        return false;
    }

    private void ShowParentReorder(DragEventArgs e, ContinueUntilBlock flow, bool hasPalette)
    {
        if (Vm is null
            || !BlockTree.TryFindOwner(Vm.Blocks, flow, out var parentOwner, out var flowIndex))
        {
            e.Effects = DragDropEffects.None;
            _gaps.Clear();
            return;
        }

        if (hasPalette == false
            && e.Data.GetData(DragFormats.ScriptBlock) is MacroBlock src
            && src is ContinueUntilBlock mf
            && (ReferenceEquals(src, flow) || BlockTree.ContainsBlock(mf, flow)))
        {
            e.Effects = DragDropEffects.None;
            _gaps.Clear();
            return;
        }

        var parentList = FindItemsControlForOwner(parentOwner) ?? ScriptList;
        if (parentList is null || !TryGetFlowChrome(e, flow, out var chrome))
        {
            e.Effects = DragDropEffects.None;
            _gaps.Clear();
            return;
        }

        var insertIndex = e.GetPosition(chrome).Y < chrome.ActualHeight / 2.0
            ? flowIndex
            : flowIndex + 1;
        e.Effects = hasPalette ? DragDropEffects.Copy : DragDropEffects.Move;
        _gaps.Show(parentList, parentOwner, insertIndex);
    }

    private void ShowBodyTarget(
        DragEventArgs e,
        ContinueUntilBlock flow,
        bool hasPalette,
        ItemsControl? list = null)
    {
        if (hasPalette == false
            && e.Data.GetData(DragFormats.ScriptBlock) is MacroBlock source
            && source is ContinueUntilBlock movingFlow
            && BlockTree.IsOwnedBy(flow.Children, movingFlow))
        {
            e.Effects = DragDropEffects.None;
            _gaps.Clear();
            return;
        }

        e.Effects = hasPalette ? DragDropEffects.Copy : DragDropEffects.Move;

        if (flow.Children.Count == 0)
        {
            var body = FindFlowBody(e, flow);
            if (body is not null)
            {
                _gaps.ShowEmptyBody(body, flow.Children);
                return;
            }
        }

        var itemsControl = list
                           ?? FindItemsControlForOwner(flow.Children)
                           ?? FindItemsControl(e.OriginalSource as DependencyObject);
        if (itemsControl is null)
        {
            _gaps.Clear();
            return;
        }

        var insertIndex = _gaps.ComputeInsertIndex(itemsControl, e.GetPosition(itemsControl));
        _gaps.Show(itemsControl, flow.Children, insertIndex);
    }

    private bool TryGetFlowChrome(DragEventArgs e, ContinueUntilBlock flow, out Border chrome)
    {
        chrome = null!;
        var current = e.OriginalSource as DependencyObject;
        while (current is not null)
        {
            if (current is Border border
                && border.Tag as string == "FlowChrome"
                && ReferenceEquals(border.DataContext, flow))
            {
                chrome = border;
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        if (FindFlowChromeInList(flow) is { } found)
        {
            chrome = found;
            return true;
        }

        return false;
    }

    private Border? FindFlowBody(DragEventArgs e, ContinueUntilBlock flow)
    {
        if (TryGetFlowChrome(e, flow, out var chrome))
        {
            return InsertionGapController.FindDescendantBorder(chrome, "FlowBody");
        }

        // Walk up from source looking for FlowBody with matching DataContext.
        var current = e.OriginalSource as DependencyObject;
        while (current is not null)
        {
            if (current is Border border
                && border.Tag as string == "FlowBody"
                && ReferenceEquals(border.DataContext, flow))
            {
                return border;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return FindFlowChromeInList(flow) is { } found
            ? InsertionGapController.FindDescendantBorder(found, "FlowBody")
            : null;
    }

    private Border? FindFlowChromeInList(ContinueUntilBlock flow)
    {
        if (!BlockTree.TryFindOwner(Vm!.Blocks, flow, out var owner, out var index))
        {
            return null;
        }

        var list = FindItemsControlForOwner(owner);
        if (list?.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement container)
        {
            return null;
        }

        return InsertionGapController.FindDescendantBorder(container, "FlowChrome");
    }

    private static bool IsNearFlowVerticalEdge(FrameworkElement chrome, Point posInChrome)
    {
        var height = chrome.ActualHeight;
        if (height <= FlowEdgePixels * 2)
        {
            // Tiny chrome: treat outer thirds as edges.
            return posInChrome.Y < height / 3.0 || posInChrome.Y > height * 2.0 / 3.0;
        }

        return posInChrome.Y < FlowEdgePixels || posInChrome.Y > height - FlowEdgePixels;
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

    private void LibraryItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm is null || !CanDragEdit())
        {
            return;
        }

        var item = FindDataContext<MacroScript>(e.OriginalSource as DependencyObject);
        if (item is null)
        {
            return;
        }

        _libraryDragStart = e.GetPosition(null);
        _libraryDragPending = true;
        _libraryDragScript = item;
    }

    private void LibraryItem_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_libraryDragPending
            || _libraryDragScript is null
            || e.LeftButton != MouseButtonState.Pressed
            || !HasDragMoved(_libraryDragStart, e.GetPosition(null)))
        {
            return;
        }

        _libraryDragPending = false;
        var data = new DataObject(DragFormats.LibraryScript, _libraryDragScript);
        DragDrop.DoDragDrop(sender as DependencyObject ?? this, data, DragDropEffects.Copy);
        _libraryDragScript = null;
    }

    private void LibraryItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _libraryDragPending = false;
        _libraryDragScript = null;
    }

    private void GraphCanvas_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DragFormats.LibraryScript)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void GraphCanvas_Drop(object sender, DragEventArgs e)
    {
        if (Vm is null
            || !e.Data.GetDataPresent(DragFormats.LibraryScript)
            || e.Data.GetData(DragFormats.LibraryScript) is not MacroScript script)
        {
            return;
        }

        var pos = e.GetPosition(GraphCanvas);
        Vm.FlowGraphVm.AddScriptNode(script, pos.X - 80, pos.Y - 36);
        e.Handled = true;
    }

    private void GraphCanvas_BackgroundDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == GraphCanvas || e.OriginalSource == GraphScroll)
        {
            Vm?.FlowGraphVm.SelectNode(null);
            Vm?.FlowGraphVm.SelectEdge(null);
            Vm?.FlowGraphVm.CancelWire();
        }
    }

    private void GraphCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (Vm is null)
        {
            return;
        }

        var pos = e.GetPosition(GraphCanvas);
        if (Vm.FlowGraphVm.IsWiring)
        {
            Vm.FlowGraphVm.UpdateWireEnd(pos);
        }

        if (_graphDraggingNode is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            var delta = pos - _graphNodeDragStart;
            if (!_graphNodeDragMoved && delta.Length > 3)
            {
                _graphNodeDragMoved = true;
            }

            if (_graphNodeDragMoved)
            {
                Vm.FlowGraphVm.MoveNode(
                    _graphDraggingNode,
                    _graphNodeOrigin.X + delta.X,
                    _graphNodeOrigin.Y + delta.Y);
            }
        }
    }

    private void GraphCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndGraphNodeDrag();
    }

    private void GraphNode_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm is null || sender is not FrameworkElement fe || fe.DataContext is not FlowGraphNode node)
        {
            return;
        }

        // Port clicks handle wiring separately.
        if (e.OriginalSource is FrameworkElement { Tag: string })
        {
            return;
        }

        Vm.FlowGraphVm.SelectNode(node);
        ScriptPanel.Focus();

        if (Vm.FlowGraphVm.IsWiring)
        {
            // Complete wire onto node body as Next/Then/Else target.
            var port = node.Kind == FlowGraphNodeKind.If ? FlowGraphPort.Next : FlowGraphPort.Next;
            Vm.FlowGraphVm.CompleteWire(node, port);
            e.Handled = true;
            return;
        }

        if (!CanDragEdit())
        {
            return;
        }

        _graphDraggingNode = node;
        _graphNodeDragStart = e.GetPosition(GraphCanvas);
        _graphNodeOrigin = new Point(node.X, node.Y);
        _graphNodeDragMoved = false;
        fe.CaptureMouse();
        e.Handled = true;
    }

    private void GraphNode_MouseMove(object sender, MouseEventArgs e)
    {
        // Canvas-level move handles dragging while captured.
    }

    private void GraphNode_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            fe.ReleaseMouseCapture();
        }

        EndGraphNodeDrag();
        e.Handled = true;
    }

    private void EndGraphNodeDrag()
    {
        if (Vm is null || _graphDraggingNode is null)
        {
            _graphDraggingNode = null;
            return;
        }

        if (_graphNodeDragMoved)
        {
            var node = _graphDraggingNode;
            var x = node.X;
            var y = node.Y;
            // Restore then mutate so undo captures the move.
            node.X = _graphNodeOrigin.X;
            node.Y = _graphNodeOrigin.Y;
            Vm.FlowGraphVm.CheckpointMove(() =>
            {
                node.X = x;
                node.Y = y;
            });
            Vm.FlowGraphVm.RebuildEdgeVisuals();
        }

        _graphDraggingNode = null;
        _graphNodeDragMoved = false;
    }

    private void GraphPort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm is null
            || sender is not FrameworkElement port
            || port.Tag is not string tag
            || FindDataContext<FlowGraphNode>(port) is not { } node)
        {
            return;
        }

        e.Handled = true;
        ScriptPanel.Focus();

        if (tag == "ConditionIn")
        {
            if (Vm.FlowGraphVm.IsWiring)
            {
                Vm.FlowGraphVm.CompleteWire(node, FlowGraphPort.Condition);
            }

            return;
        }

        var outputPort = tag switch
        {
            "Next" => FlowGraphPort.Next,
            "Condition" => FlowGraphPort.Condition,
            "Then" => FlowGraphPort.Then,
            "Else" => FlowGraphPort.Else,
            _ => (FlowGraphPort?)null
        };

        if (outputPort is null)
        {
            return;
        }

        if (Vm.FlowGraphVm.IsWiring)
        {
            Vm.FlowGraphVm.CompleteWire(node, outputPort.Value);
            return;
        }

        Vm.FlowGraphVm.BeginWire(node, outputPort.Value);
    }

    private void GraphEdge_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm is null || sender is not FrameworkElement fe || fe.DataContext is not GraphEdgeVisual visual)
        {
            return;
        }

        Vm.FlowGraphVm.SelectEdge(visual.Edge);
        ScriptPanel.Focus();
        e.Handled = true;
    }
}

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MacroBlocks.Models;

namespace MacroBlocks;

/// <summary>
/// Nudges items apart at the prospective drop index during a drag.
/// Insert index is chosen from mouse Y against resting (un-nudged) slot positions
/// so the gap itself does not fight hit-testing.
/// </summary>
internal sealed class InsertionGapController
{
    public const double GapPixels = 30;

    private ItemsControl? _activeList;
    private int _activeIndex = -1;

    public ObservableCollection<MacroBlock>? TargetOwner { get; private set; }

    public int InsertIndex { get; private set; } = -1;

    public void Show(ItemsControl list, ObservableCollection<MacroBlock> owner, int insertIndex)
    {
        if (ReferenceEquals(_activeList, list) && _activeIndex == insertIndex && ReferenceEquals(TargetOwner, owner))
        {
            return;
        }

        ClearVisualOnly();
        TargetOwner = owner;
        InsertIndex = Math.Clamp(insertIndex, 0, owner.Count);
        _activeList = list;
        _activeIndex = InsertIndex;
        Apply(list, InsertIndex);
    }

    public void Clear()
    {
        ClearVisualOnly();
        _activeList = null;
        _activeIndex = -1;
        TargetOwner = null;
        InsertIndex = -1;
    }

    /// <summary>
    /// Picks the closest insertion slot to the pointer using resting geometry
    /// (current layout with the active gap mathematically removed).
    /// </summary>
    public int ComputeInsertIndex(ItemsControl list, Point positionInList)
    {
        list.UpdateLayout();
        var count = list.Items.Count;
        if (count == 0)
        {
            return 0;
        }

        var slots = new double[count + 1];
        var haveBounds = false;

        for (var i = 0; i < count; i++)
        {
            if (!TryGetRestingBounds(list, i, out var top, out var bottom))
            {
                continue;
            }

            haveBounds = true;
            if (i == 0)
            {
                slots[0] = top;
            }

            slots[i + 1] = bottom;

            if (i > 0 && TryGetRestingBounds(list, i - 1, out _, out var prevBottom))
            {
                slots[i] = (prevBottom + top) / 2.0;
            }
        }

        if (!haveBounds)
        {
            return count;
        }

        var mouseY = positionInList.Y;
        var bestIndex = 0;
        var bestDist = Math.Abs(mouseY - slots[0]);

        for (var i = 1; i <= count; i++)
        {
            var dist = Math.Abs(mouseY - slots[i]);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private void ClearVisualOnly()
    {
        if (_activeList is not null)
        {
            Reset(_activeList);
        }
    }

    private static bool TryGetRestingBounds(ItemsControl list, int index, out double top, out double bottom)
    {
        top = 0;
        bottom = 0;

        var chrome = FindChrome(list, index);
        if (chrome is null)
        {
            return false;
        }

        Point origin;
        try
        {
            origin = chrome.TransformToAncestor(list).Transform(new Point(0, 0));
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        var baseMargin = GetBaseMargin(chrome);
        var extraTop = Math.Max(0, chrome.Margin.Top - baseMargin.Top);
        var extraBottom = Math.Max(0, chrome.Margin.Bottom - baseMargin.Bottom);

        // Border Y is already after Margin.Top; undo gap so slots stay stable while nudging.
        top = origin.Y - extraTop;
        bottom = top + chrome.ActualHeight + baseMargin.Bottom;
        _ = extraBottom; // bottom gap only stretches space below; resting block box ignores it
        return true;
    }

    private static void Apply(ItemsControl list, int insertIndex)
    {
        list.UpdateLayout();
        var count = list.Items.Count;

        for (var i = 0; i < count; i++)
        {
            var chrome = FindChrome(list, i);
            if (chrome is null)
            {
                continue;
            }

            var baseMargin = GetBaseMargin(chrome);
            if (i == insertIndex)
            {
                chrome.Margin = new Thickness(baseMargin.Left, GapPixels, baseMargin.Right, baseMargin.Bottom);
                chrome.BorderBrush = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA));
            }
            else if (insertIndex == count && i == count - 1)
            {
                chrome.Margin = new Thickness(baseMargin.Left, baseMargin.Top, baseMargin.Right, GapPixels);
                chrome.BorderBrush = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA));
            }
            else
            {
                chrome.Margin = baseMargin;
                RestoreBorder(chrome);
            }
        }
    }

    private static void Reset(ItemsControl list)
    {
        list.UpdateLayout();
        for (var i = 0; i < list.Items.Count; i++)
        {
            var chrome = FindChrome(list, i);
            if (chrome is null)
            {
                continue;
            }

            chrome.Margin = GetBaseMargin(chrome);
            RestoreBorder(chrome);
        }
    }

    private static Thickness GetBaseMargin(FrameworkElement chrome)
    {
        var bottom = chrome.Tag as string == "FlowChrome" ? 8d : 6d;
        return new Thickness(0, 0, 0, bottom);
    }

    private static void RestoreBorder(Border chrome)
    {
        chrome.BorderBrush = chrome.Tag as string == "FlowChrome"
            ? new SolidColorBrush(Color.FromRgb(0x93, 0xC5, 0xFD))
            : new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB));
    }

    private static Border? FindChrome(ItemsControl list, int index)
    {
        if (list.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement container)
        {
            return null;
        }

        return FindDescendantBorder(container, "BlockChrome")
               ?? FindDescendantBorder(container, "FlowChrome");
    }

    private static Border? FindDescendantBorder(DependencyObject root, string tag)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Border border && border.Tag as string == tag)
            {
                return border;
            }

            var nested = FindDescendantBorder(child, tag);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BlockMacro.Models;

namespace BlockMacro;

/// <summary>
/// Nudges items apart at the prospective drop index during a drag.
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

        Clear();
        TargetOwner = owner;
        InsertIndex = Math.Clamp(insertIndex, 0, owner.Count);
        _activeList = list;
        _activeIndex = InsertIndex;
        Apply(list, InsertIndex);
    }

    public void Clear()
    {
        if (_activeList is not null)
        {
            Reset(list: _activeList);
        }

        _activeList = null;
        _activeIndex = -1;
        TargetOwner = null;
        InsertIndex = -1;
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
        // Continue Until outer chrome uses 8 bottom; default blocks use 6.
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

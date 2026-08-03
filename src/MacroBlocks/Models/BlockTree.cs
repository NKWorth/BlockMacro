using System.Collections.ObjectModel;

namespace MacroBlocks.Models;

/// <summary>
/// Helpers for walking and mutating the nested block tree.
/// </summary>
public static class BlockTree
{
    public static IEnumerable<MacroBlock> Enumerate(IEnumerable<MacroBlock> blocks)
    {
        foreach (var block in blocks)
        {
            yield return block;
            if (block is ContinueUntilBlock flow)
            {
                foreach (var child in Enumerate(flow.Children))
                {
                    yield return child;
                }
            }
        }
    }

    public static IEnumerable<EventBlock> EnumerateEvents(IEnumerable<MacroBlock> blocks)
        => Enumerate(blocks).OfType<EventBlock>();

    public static IEnumerable<RunSubscriptBlock> EnumerateSubscripts(IEnumerable<MacroBlock> blocks)
        => Enumerate(blocks).OfType<RunSubscriptBlock>();

    public static bool TryFindOwner(
        ObservableCollection<MacroBlock> root,
        MacroBlock block,
        out ObservableCollection<MacroBlock> owner,
        out int index)
    {
        for (var i = 0; i < root.Count; i++)
        {
            if (ReferenceEquals(root[i], block))
            {
                owner = root;
                index = i;
                return true;
            }

            if (root[i] is ContinueUntilBlock flow
                && TryFindOwner(flow.Children, block, out owner!, out index))
            {
                return true;
            }
        }

        owner = root;
        index = -1;
        return false;
    }

    public static bool ContainsBlock(ContinueUntilBlock flow, MacroBlock block)
    {
        foreach (var child in flow.Children)
        {
            if (ReferenceEquals(child, block))
            {
                return true;
            }

            if (child is ContinueUntilBlock nested && ContainsBlock(nested, block))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsOwnedBy(ObservableCollection<MacroBlock> collection, ContinueUntilBlock flow)
    {
        if (ReferenceEquals(collection, flow.Children))
        {
            return true;
        }

        foreach (var child in flow.Children.OfType<ContinueUntilBlock>())
        {
            if (IsOwnedBy(collection, child))
            {
                return true;
            }
        }

        return false;
    }
}

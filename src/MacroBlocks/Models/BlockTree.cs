using System.Collections.ObjectModel;

namespace MacroBlocks.Models;

/// <summary>
/// Where a block lives in the nested script tree.
/// </summary>
public abstract record BlockLocation;

public sealed record CollectionLocation(ObservableCollection<MacroBlock> Owner, int Index) : BlockLocation;

public sealed record EventSlotLocation(ContinueUntilBlock Flow) : BlockLocation;

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
                if (flow.EventSlot is not null)
                {
                    yield return flow.EventSlot;
                }

                foreach (var child in Enumerate(flow.Children))
                {
                    yield return child;
                }
            }
        }
    }

    public static IEnumerable<EventBlock> EnumerateEvents(IEnumerable<MacroBlock> blocks)
        => Enumerate(blocks).OfType<EventBlock>();

    /// <summary>
    /// Events that live in collections (root or body), not in a Continue Until event slot.
    /// </summary>
    public static IEnumerable<EventBlock> EnumerateFreeEvents(IEnumerable<MacroBlock> blocks)
    {
        foreach (var block in blocks)
        {
            if (block is EventBlock evt)
            {
                yield return evt;
            }

            if (block is ContinueUntilBlock flow)
            {
                foreach (var nested in EnumerateFreeEvents(flow.Children))
                {
                    yield return nested;
                }
            }
        }
    }

    public static IEnumerable<RunSubscriptBlock> EnumerateSubscripts(IEnumerable<MacroBlock> blocks)
        => Enumerate(blocks).OfType<RunSubscriptBlock>();

    public static bool TryLocate(
        ObservableCollection<MacroBlock> root,
        MacroBlock block,
        out BlockLocation location)
    {
        for (var i = 0; i < root.Count; i++)
        {
            if (ReferenceEquals(root[i], block))
            {
                location = new CollectionLocation(root, i);
                return true;
            }

            if (root[i] is not ContinueUntilBlock flow)
            {
                continue;
            }

            if (ReferenceEquals(flow.EventSlot, block))
            {
                location = new EventSlotLocation(flow);
                return true;
            }

            if (TryLocate(flow.Children, block, out location!))
            {
                return true;
            }
        }

        location = null!;
        return false;
    }

    public static bool TryFindOwner(
        ObservableCollection<MacroBlock> root,
        MacroBlock block,
        out ObservableCollection<MacroBlock> owner,
        out int index)
    {
        if (TryLocate(root, block, out var location) && location is CollectionLocation col)
        {
            owner = col.Owner;
            index = col.Index;
            return true;
        }

        owner = root;
        index = -1;
        return false;
    }

    public static bool TryFindEventSlotOwner(
        ObservableCollection<MacroBlock> root,
        EventBlock evt,
        out ContinueUntilBlock flow)
    {
        if (TryLocate(root, evt, out var location) && location is EventSlotLocation slot)
        {
            flow = slot.Flow;
            return true;
        }

        flow = null!;
        return false;
    }

    public static bool ContainsBlock(ContinueUntilBlock flow, MacroBlock block)
    {
        if (ReferenceEquals(flow.EventSlot, block))
        {
            return true;
        }

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

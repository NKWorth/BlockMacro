using System.Collections.ObjectModel;

namespace MacroBlocks.Models;

/// <summary>
/// Converts legacy flat Continue Until / End Continue markers into nested Children,
/// and absorbs legacy EventBlockId references into owned event slots.
/// </summary>
public static class ScriptMigrator
{
    public static List<MacroBlock> ToNested(IReadOnlyList<MacroBlock> flat)
    {
        var (blocks, _) = ParseRange(flat, 0, flat.Count);
        AbsorbEventSlots(blocks);
        return blocks;
    }

    public static void AbsorbEventSlots(IList<MacroBlock> root)
    {
        // Snapshot flows first — moving events mutates collections.
        foreach (var flow in EnumerateFlows(root).ToList())
        {
            if (flow.EventSlot is not null)
            {
                flow.EventLabel = flow.EventSlot.Name;
                flow.ClearLegacyEventBlockId();
                continue;
            }

            if (flow.LegacyEventBlockId is not { } id)
            {
                continue;
            }

            if (!TryRemoveEventById(root, id, out var evt))
            {
                flow.EventLabel = "(missing event)";
                flow.ClearLegacyEventBlockId();
                continue;
            }

            flow.EventSlot = evt;
            flow.ClearLegacyEventBlockId();
        }
    }

    private static IEnumerable<ContinueUntilBlock> EnumerateFlows(IEnumerable<MacroBlock> blocks)
    {
        foreach (var block in blocks)
        {
            if (block is ContinueUntilBlock flow)
            {
                yield return flow;
                foreach (var nested in EnumerateFlows(flow.Children))
                {
                    yield return nested;
                }
            }
        }
    }

    private static bool TryRemoveEventById(IList<MacroBlock> blocks, Guid id, out EventBlock evt)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            if (blocks[i] is EventBlock found && found.Id == id)
            {
                evt = found;
                blocks.RemoveAt(i);
                return true;
            }

            if (blocks[i] is ContinueUntilBlock flow)
            {
                if (flow.EventSlot?.Id == id)
                {
                    // Already owned by some flow; leave it (caller only runs when slot empty).
                    evt = null!;
                    return false;
                }

                if (TryRemoveEventById(flow.Children, id, out evt))
                {
                    return true;
                }
            }
        }

        evt = null!;
        return false;
    }

    private static (List<MacroBlock> Blocks, int NextIndex) ParseRange(
        IReadOnlyList<MacroBlock> flat,
        int start,
        int end)
    {
        var result = new List<MacroBlock>();

        for (var i = start; i < end;)
        {
            switch (flat[i])
            {
                case EndContinueBlock:
                    return (result, i);

                case ContinueUntilBlock continueUntil:
                    NormalizeContinueUntil(continueUntil, flat, ref i, end);
                    result.Add(continueUntil);
                    break;

                default:
                    result.Add(flat[i]);
                    i++;
                    break;
            }
        }

        return (result, end);
    }

    private static void NormalizeContinueUntil(
        ContinueUntilBlock continueUntil,
        IReadOnlyList<MacroBlock> flat,
        ref int index,
        int end)
    {
        if (continueUntil.Children.Count > 0)
        {
            var nested = ParseRange(continueUntil.Children.ToList(), 0, continueUntil.Children.Count).Blocks;
            continueUntil.Children.Clear();
            foreach (var child in nested)
            {
                continueUntil.Children.Add(child);
            }

            index++;
            return;
        }

        var (body, next) = ParseRange(flat, index + 1, end);
        foreach (var child in body)
        {
            continueUntil.Children.Add(child);
        }

        // Skip matching End Continue when present.
        index = next < end && flat[next] is EndContinueBlock ? next + 1 : next;
    }
}

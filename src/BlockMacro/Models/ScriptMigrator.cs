using System.Collections.ObjectModel;

namespace BlockMacro.Models;

/// <summary>
/// Converts legacy flat Continue Until / End Continue markers into nested Children.
/// </summary>
public static class ScriptMigrator
{
    public static List<MacroBlock> ToNested(IReadOnlyList<MacroBlock> flat)
    {
        var (blocks, _) = ParseRange(flat, 0, flat.Count);
        return blocks;
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
            var nested = ToNested(continueUntil.Children.ToList());
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

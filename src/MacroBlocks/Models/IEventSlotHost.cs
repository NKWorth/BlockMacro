using MacroBlocks.Models.Events;

namespace MacroBlocks.Models;

/// <summary>
/// A block that can own a single event in a dedicated slot (not part of its body).
/// </summary>
public interface IEventSlotHost
{
    EventBlock? EventSlot { get; set; }

    bool HasEventSlot { get; }
}

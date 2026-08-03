using MacroBlocks.Models;

namespace MacroBlocks.Services.Persistence;

public interface IScriptLibrary
{
    IReadOnlyList<MacroScript> List();

    MacroScript? Get(Guid id);

    void Save(MacroScript script);

    bool Delete(Guid id);

    event EventHandler? Changed;
}

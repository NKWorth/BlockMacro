using BlockMacro.Models;

namespace BlockMacro.Services;

public interface IScriptLibrary
{
    IReadOnlyList<MacroScript> List();

    MacroScript? Get(Guid id);

    void Save(MacroScript script);

    bool Delete(Guid id);

    event EventHandler? Changed;
}

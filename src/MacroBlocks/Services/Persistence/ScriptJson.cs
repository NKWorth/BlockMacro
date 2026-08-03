using System.Text.Json;
using System.Text.Json.Serialization;
using MacroBlocks.Models;

namespace MacroBlocks.Services.Persistence;

public static class ScriptJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(MacroScript script)
        => JsonSerializer.Serialize(script, Options);

    public static MacroScript Deserialize(string json)
        => JsonSerializer.Deserialize<MacroScript>(json, Options)
           ?? throw new InvalidOperationException("Script JSON deserialized to null.");
}

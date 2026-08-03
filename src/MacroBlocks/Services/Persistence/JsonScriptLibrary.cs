using System.IO;
using MacroBlocks.Models;

namespace MacroBlocks.Services.Persistence;

/// <summary>
/// Persists reusable scripts as JSON files under LocalApplicationData/MacroBlocks/library.
/// </summary>
public sealed class JsonScriptLibrary : IScriptLibrary
{
    private readonly string _directory;
    private readonly object _gate = new();

    public JsonScriptLibrary(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MacroBlocks",
            "library");

        MigrateLegacyLibraryIfNeeded(_directory);
        Directory.CreateDirectory(_directory);
    }

    private static void MigrateLegacyLibraryIfNeeded(string newDirectory)
    {
        if (Directory.Exists(newDirectory) && Directory.EnumerateFileSystemEntries(newDirectory).Any())
        {
            return;
        }

        var legacyDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BlockMacro",
            "library");

        if (!Directory.Exists(legacyDirectory))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(newDirectory)!);
        try
        {
            Directory.Move(legacyDirectory, newDirectory);
        }
        catch
        {
            // Fall back to copy if move fails (e.g. cross-volume or locked files).
            Directory.CreateDirectory(newDirectory);
            foreach (var file in Directory.EnumerateFiles(legacyDirectory, "*.json"))
            {
                File.Copy(file, Path.Combine(newDirectory, Path.GetFileName(file)), overwrite: false);
            }
        }
    }

    public event EventHandler? Changed;

    public IReadOnlyList<MacroScript> List()
    {
        lock (_gate)
        {
            return Directory.EnumerateFiles(_directory, "*.json")
                .Select(TryLoadFile)
                .Where(s => s is not null)
                .Cast<MacroScript>()
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public MacroScript? Get(Guid id)
    {
        lock (_gate)
        {
            var path = GetPath(id);
            return File.Exists(path) ? TryLoadFile(path) : null;
        }
    }

    public void Save(MacroScript script)
    {
        ArgumentNullException.ThrowIfNull(script);

        if (string.IsNullOrWhiteSpace(script.Name))
        {
            script.Name = "Untitled Macro";
        }

        script.UpdatedAt = DateTimeOffset.UtcNow;
        var snapshot = script.CloneDeep();
        var json = ScriptJson.Serialize(snapshot);

        lock (_gate)
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(GetPath(snapshot.Id), json);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Delete(Guid id)
    {
        lock (_gate)
        {
            var path = GetPath(id);
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private string GetPath(Guid id) => Path.Combine(_directory, $"{id:N}.json");

    private static MacroScript? TryLoadFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return ScriptJson.Deserialize(json);
        }
        catch
        {
            return null;
        }
    }
}

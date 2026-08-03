using System.IO;

namespace MacroBlocks.Services.Vision;

/// <summary>
/// Stores template images under LocalApplicationData/MacroBlocks/images.
/// </summary>
public static class TemplateImageStore
{
    public static string DirectoryPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MacroBlocks",
        "images");

    public static string EnsureDirectory()
    {
        Directory.CreateDirectory(DirectoryPath);
        return DirectoryPath;
    }

    public static string Import(string sourceFilePath)
    {
        EnsureDirectory();
        var ext = Path.GetExtension(sourceFilePath);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = ".png";
        }

        var name = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var dest = Path.Combine(DirectoryPath, name);
        File.Copy(sourceFilePath, dest, overwrite: true);
        return name;
    }

    public static string? ResolvePath(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var path = Path.Combine(DirectoryPath, fileName);
        return File.Exists(path) ? path : null;
    }
}

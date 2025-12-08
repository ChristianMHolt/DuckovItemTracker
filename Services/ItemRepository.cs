using System.IO;
using System.Linq;
using System.Text.Json;
using ItemTracker.Models;

namespace ItemTracker.Services;

public class ItemRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private const string ExternalBasePath = @"X:\\Software\\DuckovItemTrackerCSharp";

    private readonly string _dataFilePath;
    public string DefaultImageFolder { get; }

    public ItemRepository()
    {
        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _dataFilePath = Path.Combine(baseDir, "items.json");
        DefaultImageFolder = Path.Combine(baseDir, "ItemPNGS");
    }

    public IEnumerable<Item> Load()
    {
        if (!File.Exists(_dataFilePath))
        {
            return Enumerable.Empty<Item>();
        }

        try
        {
            var text = File.ReadAllText(_dataFilePath);
            var items = JsonSerializer.Deserialize<List<Item>>(text, JsonOptions);
            return items ?? Enumerable.Empty<Item>();
        }
        catch
        {
            return Enumerable.Empty<Item>();
        }
    }

    public void Save(IEnumerable<Item> items)
    {
        var directory = Path.GetDirectoryName(_dataFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(items, JsonOptions);
        File.WriteAllText(_dataFilePath, json);
    }

    public void CopyDataFileAndItemIcon(Item item)
    {
        CopyDataFile();
        CopyItemIcon(item);
    }

    public void CopyDataFile()
    {
        if (!File.Exists(_dataFilePath))
        {
            return;
        }

        Directory.CreateDirectory(ExternalBasePath);
        var destinationPath = Path.Combine(ExternalBasePath, "items.json");
        File.Copy(_dataFilePath, destinationPath, overwrite: true);
    }

    private void CopyItemIcon(Item item)
    {
        if (string.IsNullOrWhiteSpace(item.IconPath))
        {
            return;
        }

        var sourceImagePath = item.IconPath;
        if (!File.Exists(sourceImagePath))
        {
            return;
        }

        var destinationFolder = Path.Combine(ExternalBasePath, "ItemPNGS");
        Directory.CreateDirectory(destinationFolder);

        var destinationPath = Path.Combine(destinationFolder, Path.GetFileName(sourceImagePath));
        File.Copy(sourceImagePath, destinationPath, overwrite: true);
    }

    public IReadOnlyList<string> FindUnusedImages(IEnumerable<Item> items)
    {
        if (!Directory.Exists(DefaultImageFolder))
        {
            return Array.Empty<string>();
        }

        var used = new HashSet<string>(
            items.Where(i => !string.IsNullOrWhiteSpace(i.IconPath))
                .Select(i => Path.GetFileName(i.IconPath!)?.ToLowerInvariant() ?? string.Empty)
                .Where(name => !string.IsNullOrEmpty(name)));

        var folderFiles = Directory
            .EnumerateFiles(DefaultImageFolder)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!.ToLowerInvariant());

        return folderFiles
            .Where(name => !used.Contains(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

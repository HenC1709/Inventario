using System.IO;
using System.Text.Json;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.Infrastructure.Persistence.Json;

/// <summary>
/// JSON-backed data context managing in-memory collections, settings, and atomic persistence under the data directory.
/// </summary>
public class JsonDataContext
{
    private readonly string _dataDirectory;

    public List<Supplier> Suppliers { get; private set; } = new();
    public List<Product> Products { get; private set; } = new();
    public List<StockMovement> StockMovements { get; private set; } = new();
    public AppSettings Settings { get; private set; } = new();

    /// <summary>
    /// Initializes a new JSON data context targeting the given directory, or the default application data folder.
    /// </summary>
    public JsonDataContext(string? dataDirectory = null)
    {
        _dataDirectory = AppDataPaths.ResolveDataDirectory(dataDirectory);
    }

    /// <summary>
    /// Replaces the in-memory settings instance prior to persistence.
    /// </summary>
    public void UpdateSettings(AppSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Persists suppliers, products, stock movements, and settings to JSON files using atomic file replacement.
    /// </summary>
    public void SaveJson()
    {
        Directory.CreateDirectory(_dataDirectory);

        var options = new JsonSerializerOptions { WriteIndented = true };

        WriteFileAtomically(Path.Combine(_dataDirectory, "proveedores.json"), JsonSerializer.Serialize(Suppliers, options));
        WriteFileAtomically(Path.Combine(_dataDirectory, "productos.json"), JsonSerializer.Serialize(Products, options));
        WriteFileAtomically(Path.Combine(_dataDirectory, "tickets.json"), JsonSerializer.Serialize(StockMovements, options));
        WriteFileAtomically(Path.Combine(_dataDirectory, "settings.json"), JsonSerializer.Serialize(Settings, options));
    }

    /// <summary>
    /// Loads suppliers, products, single-warehouse stock movements, and settings from JSON files in the data directory.
    /// </summary>
    public void LoadJson()
    {
        Directory.CreateDirectory(_dataDirectory);

        var suppliersPath = Path.Combine(_dataDirectory, "proveedores.json");
        var productsPath = Path.Combine(_dataDirectory, "productos.json");
        var movementsPath = Path.Combine(_dataDirectory, "tickets.json");
        var settingsPath = Path.Combine(_dataDirectory, "settings.json");

        if (File.Exists(suppliersPath))
        {
            var json = File.ReadAllText(suppliersPath);
            Suppliers = JsonSerializer.Deserialize<List<Supplier>>(json) ?? new();
        }

        if (File.Exists(productsPath))
        {
            var json = File.ReadAllText(productsPath);
            Products = JsonSerializer.Deserialize<List<Product>>(json) ?? new();
        }

        if (File.Exists(movementsPath))
        {
            var json = File.ReadAllText(movementsPath);
            var loadedMovements = JsonSerializer.Deserialize<List<StockMovement>>(json) ?? new();
            StockMovements = loadedMovements
                .Where(m => m.MovementType is MovementType.Entry or MovementType.Exit)
                .ToList();
        }

        if (File.Exists(settingsPath))
        {
            var json = File.ReadAllText(settingsPath);
            Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new();
        }
    }

    /// <summary>
    /// Writes content to a temporary file and atomically replaces the target file to avoid partial writes on failure.
    /// </summary>
    private static void WriteFileAtomically(string filePath, string content)
    {
        var tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, content);
        File.Move(tempPath, filePath, overwrite: true);
    }
}

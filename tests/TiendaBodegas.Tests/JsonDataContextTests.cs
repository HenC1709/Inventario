using System.IO;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Enums;
using TiendaBodegas.Infrastructure.Persistence.Json;
using TiendaBodegas.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TiendaBodegas.Tests;

/// <summary>
/// Integration tests verifying JSON persistence and round-trip deserialization in <see cref="JsonDataContext"/>.
/// </summary>
public class JsonDataContextTests : IDisposable
{
    private readonly string _tempDirectory;

    /// <summary>
    /// Initializes an isolated temporary directory for each test run.
    /// </summary>
    public JsonDataContextTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "TiendaBodegasTests_" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Verifies that saving and reloading JSON preserves all entity fields including private-setter properties.
    /// </summary>
    [Fact]
    public void SaveAndLoadJson_PreservesAllEntityProperties()
    {
        var writeContext = new JsonDataContext(_tempDirectory);
        var warehouseRepo = new WarehouseRepository(writeContext);
        var productRepo = new ProductRepository(writeContext);
        var movementRepo = new StockMovementRepository(writeContext);

        var warehouse = new Warehouse("Bodega Central", "Av. Principal 100");
        warehouseRepo.Add(warehouse);

        var product = new Product("Arroz 1kg", "ARR-001", 12.50m, minStock: 10);
        productRepo.Add(product);

        var movement = new StockMovement(
            product.Id,
            25,
            MovementType.Entry,
            null,
            warehouse.Id,
            "Compra inicial");
        movementRepo.Add(movement);

        var readContext = new JsonDataContext(_tempDirectory);
        readContext.LoadJson();

        var loadedWarehouse = Assert.Single(readContext.Warehouses);
        Assert.Equal(warehouse.Id, loadedWarehouse.Id);
        Assert.Equal("Bodega Central", loadedWarehouse.Name);
        Assert.Equal("Av. Principal 100", loadedWarehouse.Address);

        var loadedProduct = Assert.Single(readContext.Products);
        Assert.Equal(product.Id, loadedProduct.Id);
        Assert.Equal("Arroz 1kg", loadedProduct.Name);
        Assert.Equal("ARR-001", loadedProduct.SKU);
        Assert.Equal(12.50m, loadedProduct.Price);
        Assert.Equal(10, loadedProduct.MinStock);

        var loadedMovement = Assert.Single(readContext.StockMovements);
        Assert.Equal(movement.Id, loadedMovement.Id);
        Assert.Equal(product.Id, loadedMovement.ProductId);
        Assert.Equal(25, loadedMovement.Quantity);
        Assert.Equal(MovementType.Entry, loadedMovement.MovementType);
        Assert.Null(loadedMovement.SourceWarehouseId);
        Assert.Equal(warehouse.Id, loadedMovement.DestinationWarehouseId);
        Assert.Equal("Compra inicial", loadedMovement.Notes);
        Assert.Equal(movement.Date, loadedMovement.Date);
    }

    /// <summary>
    /// Verifies that atomic saving writes the expected JSON files and leaves no temporary files behind.
    /// </summary>
    [Fact]
    public void SaveJson_WritesFilesAtomicallyWithoutLeavingTempFiles()
    {
        var context = new JsonDataContext(_tempDirectory);
        context.SaveJson();

        Assert.True(File.Exists(Path.Combine(_tempDirectory, "bodegas.json")));
        Assert.True(File.Exists(Path.Combine(_tempDirectory, "productos.json")));
        Assert.True(File.Exists(Path.Combine(_tempDirectory, "tickets.json")));
        Assert.Empty(Directory.GetFiles(_tempDirectory, "*.tmp"));
    }

    /// <summary>
    /// Verifies that legacy product JSON records lacking StockMinimo deserialize cleanly with MinStock set to zero.
    /// </summary>
    [Fact]
    public void LoadJson_WhenLegacyProductsLackMinStock_DefaultsToZero()
    {
        Directory.CreateDirectory(_tempDirectory);
        var legacyProductsJson = """
        [
          {
            "Id": 1,
            "Nombre": "Producto Legado",
            "SKU": "LEG-001",
            "Precio": 9.95
          }
        ]
        """;
        File.WriteAllText(Path.Combine(_tempDirectory, "productos.json"), legacyProductsJson);

        var context = new JsonDataContext(_tempDirectory);
        context.LoadJson();

        var loadedProduct = Assert.Single(context.Products);
        Assert.Equal("Producto Legado", loadedProduct.Name);
        Assert.Equal(0, loadedProduct.MinStock);
    }

    /// <summary>
    /// Cleans up the temporary test directory after execution.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }
}

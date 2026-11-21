using System.IO;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Enums;
using TiendaBodegas.Infrastructure.Persistence.Json;
using TiendaBodegas.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TiendaBodegas.Tests;

/// <summary>
/// Integration tests verifying JSON persistence, round-trip deserialization, and legacy compatibility in <see cref="JsonDataContext"/>.
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
        var supplierRepo = new SupplierRepository(writeContext);
        var productRepo = new ProductRepository(writeContext);
        var movementRepo = new StockMovementRepository(writeContext);

        var supplier = new Supplier(
            "Distribuidora del Norte S.A.",
            "contacto@distnorte.com",
            "Alimentos",
            SupplierStatus.UnderReview);
        supplierRepo.Add(supplier);

        var product = new Product(
            "Arroz 1kg",
            "ARR-001",
            "Alimentos",
            supplier.Id,
            8.00m,
            12.50m,
            minStock: 10);
        productRepo.Add(product);

        var movement = new StockMovement(
            product.Id,
            25,
            MovementType.Entry,
            "Compra inicial");
        movementRepo.Add(movement);

        var readContext = new JsonDataContext(_tempDirectory);
        readContext.LoadJson();

        var loadedSupplier = Assert.Single(readContext.Suppliers);
        Assert.Equal(supplier.Id, loadedSupplier.Id);
        Assert.Equal("PRV-01", loadedSupplier.Code);
        Assert.Equal("Distribuidora del Norte S.A.", loadedSupplier.Name);
        Assert.Equal("contacto@distnorte.com", loadedSupplier.Contact);
        Assert.Equal("Alimentos", loadedSupplier.MainCategory);
        Assert.Equal(SupplierStatus.UnderReview, loadedSupplier.Status);

        var loadedProduct = Assert.Single(readContext.Products);
        Assert.Equal(product.Id, loadedProduct.Id);
        Assert.Equal("Arroz 1kg", loadedProduct.Name);
        Assert.Equal("ARR-001", loadedProduct.SKU);
        Assert.Equal("Alimentos", loadedProduct.Category);
        Assert.Equal(supplier.Id, loadedProduct.SupplierId);
        Assert.Equal(8.00m, loadedProduct.PurchasePrice);
        Assert.Equal(12.50m, loadedProduct.SalePrice);
        Assert.Equal(10, loadedProduct.MinStock);

        var loadedMovement = Assert.Single(readContext.StockMovements);
        Assert.Equal(movement.Id, loadedMovement.Id);
        Assert.Equal(product.Id, loadedMovement.ProductId);
        Assert.Equal(25, loadedMovement.Quantity);
        Assert.Equal(MovementType.Entry, loadedMovement.MovementType);
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

        Assert.True(File.Exists(Path.Combine(_tempDirectory, "proveedores.json")));
        Assert.True(File.Exists(Path.Combine(_tempDirectory, "productos.json")));
        Assert.True(File.Exists(Path.Combine(_tempDirectory, "tickets.json")));
        Assert.Empty(Directory.GetFiles(_tempDirectory, "*.tmp"));
    }

    /// <summary>
    /// Verifies that legacy product JSON records using <c>Precio</c> map to <see cref="Product.SalePrice"/> cleanly.
    /// </summary>
    [Fact]
    public void LoadJson_WhenLegacyProductsUsePrecioAndLackNewFields_PreservesSalePriceAndDefaults()
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
        Assert.Equal("LEG-001", loadedProduct.SKU);
        Assert.Equal(9.95m, loadedProduct.SalePrice);
        Assert.Equal(0m, loadedProduct.PurchasePrice);
        Assert.Equal(string.Empty, loadedProduct.Category);
        Assert.Null(loadedProduct.SupplierId);
        Assert.Equal(0, loadedProduct.MinStock);
    }

    /// <summary>
    /// Verifies that legacy transfer movements in <c>tickets.json</c> are ignored when loading single-warehouse history.
    /// </summary>
    [Fact]
    public void LoadJson_WhenLegacyTicketsContainTransfers_IgnoresNonSingleWarehouseMovements()
    {
        Directory.CreateDirectory(_tempDirectory);
        var legacyTicketsJson = """
        [
          { "Id": 1, "Fecha": "2026-11-01T10:00:00", "ProductoId": 1, "Cantidad": 20, "Tipo": 0, "Observaciones": "Entrada" },
          { "Id": 2, "Fecha": "2026-11-01T11:00:00", "ProductoId": 1, "Cantidad": 5, "Tipo": 2, "Observaciones": "Traslado legado" },
          { "Id": 3, "Fecha": "2026-11-01T12:00:00", "ProductoId": 1, "Cantidad": 4, "Tipo": 1, "Observaciones": "Salida" }
        ]
        """;
        File.WriteAllText(Path.Combine(_tempDirectory, "tickets.json"), legacyTicketsJson);

        var context = new JsonDataContext(_tempDirectory);
        context.LoadJson();

        Assert.Equal(2, context.StockMovements.Count);
        Assert.DoesNotContain(context.StockMovements, m => m.Notes == "Traslado legado");
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

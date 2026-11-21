using System.IO;
using TiendaBodegas.Application.Services;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Infrastructure.Persistence.Json;
using TiendaBodegas.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TiendaBodegas.Tests;

/// <summary>
/// Unit tests covering CRUD operations, SKU uniqueness, and movement-protected deletion in <see cref="CatalogManager"/>.
/// </summary>
public class CatalogManagerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly JsonDataContext _context;
    private readonly CatalogManager _catalogManager;
    private readonly InventoryManager _inventoryManager;

    /// <summary>
    /// Initializes an isolated JSON context and application services for each test run.
    /// </summary>
    public CatalogManagerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "TiendaBodegasCatalogTests_" + Guid.NewGuid().ToString("N"));
        _context = new JsonDataContext(_tempDirectory);
        var warehouseRepo = new WarehouseRepository(_context);
        var productRepo = new ProductRepository(_context);
        var movementRepo = new StockMovementRepository(_context);

        _catalogManager = new CatalogManager(productRepo, warehouseRepo, movementRepo);
        _inventoryManager = new InventoryManager(movementRepo, warehouseRepo, productRepo);
    }

    /// <summary>
    /// Verifies that products and warehouses can be created, updated in place, and deleted when unused.
    /// </summary>
    [Fact]
    public void CreateUpdateAndDelete_WhenEntitiesHaveNoMovements_PersistsChanges()
    {
        var product = _catalogManager.CreateProduct("  Café Molido  ", " CAF-001 ", 4.50m, minStock: 8);
        var warehouse = _catalogManager.CreateWarehouse("  Bodega Este  ", " Av. Sol 10 ");

        _catalogManager.UpdateProduct(product.Id, "Café Premium", "CAF-001", 5.25m, minStock: 15);
        _catalogManager.UpdateWarehouse(warehouse.Id, "Bodega Este Renovada", "Av. Sol 25");

        var reloadedContext = new JsonDataContext(_tempDirectory);
        reloadedContext.LoadJson();

        var savedProduct = Assert.Single(reloadedContext.Products);
        Assert.Equal("Café Premium", savedProduct.Name);
        Assert.Equal("CAF-001", savedProduct.SKU);
        Assert.Equal(5.25m, savedProduct.Price);
        Assert.Equal(15, savedProduct.MinStock);

        var savedWarehouse = Assert.Single(reloadedContext.Warehouses);
        Assert.Equal("Bodega Este Renovada", savedWarehouse.Name);
        Assert.Equal("Av. Sol 25", savedWarehouse.Address);

        _catalogManager.DeleteProduct(product.Id);
        _catalogManager.DeleteWarehouse(warehouse.Id);

        Assert.Empty(_catalogManager.GetAllProducts());
        Assert.Empty(_catalogManager.GetAllWarehouses());
    }

    /// <summary>
    /// Verifies that duplicate SKUs are rejected case-insensitively while allowing a product to keep its own SKU on edit.
    /// </summary>
    [Fact]
    public void CreateOrUpdateProduct_WhenSkuAlreadyExists_ThrowsInvalidOperationException()
    {
        var first = _catalogManager.CreateProduct("Producto A", "SKU-100", 10m);
        var second = _catalogManager.CreateProduct("Producto B", "SKU-200", 20m);

        Assert.Throws<InvalidOperationException>(() =>
            _catalogManager.CreateProduct("Producto C", "sku-100", 15m));

        Assert.Throws<InvalidOperationException>(() =>
            _catalogManager.UpdateProduct(second.Id, "Producto B", "sku-100", 20m));

        _catalogManager.UpdateProduct(first.Id, "Producto A Editado", "SKU-100", 12m);
        Assert.Equal(12m, first.Price);
    }

    /// <summary>
    /// Verifies that products and warehouses with invalid inputs are rejected.
    /// </summary>
    [Fact]
    public void CreateProductOrWarehouse_WithInvalidInputs_ThrowsValidationExceptions()
    {
        Assert.Throws<ArgumentException>(() => _catalogManager.CreateProduct("", "SKU-1", 10m));
        Assert.Throws<ArgumentException>(() => _catalogManager.CreateProduct("Producto", " ", 10m));
        Assert.Throws<ArgumentOutOfRangeException>(() => _catalogManager.CreateProduct("Producto", "SKU-1", 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => _catalogManager.CreateProduct("Producto", "SKU-1", -3m));
        Assert.Throws<ArgumentOutOfRangeException>(() => _catalogManager.CreateProduct("Producto", "SKU-1", 5m, minStock: -1));

        var validProduct = _catalogManager.CreateProduct("Producto", "SKU-1", 5m, minStock: 2);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _catalogManager.UpdateProduct(validProduct.Id, "Producto", "SKU-1", 5m, minStock: -4));

        Assert.Throws<ArgumentException>(() => _catalogManager.CreateWarehouse("", "Dirección"));
        Assert.Throws<ArgumentException>(() => _catalogManager.CreateWarehouse("Bodega", " "));
    }

    /// <summary>
    /// Verifies that deleting a product or warehouse that already has stock movements is blocked.
    /// </summary>
    [Fact]
    public void DeleteProductOrWarehouse_WhenMovementsExist_ThrowsInvalidOperationException()
    {
        var product = _catalogManager.CreateProduct("Harina 1kg", "HAR-001", 2.00m);
        var warehouse = _catalogManager.CreateWarehouse("Bodega Central", "Av. 1");
        _inventoryManager.RegisterEntry(product.Id, warehouse.Id, 15, "Stock inicial");

        Assert.Throws<InvalidOperationException>(() => _catalogManager.DeleteProduct(product.Id));
        Assert.Throws<InvalidOperationException>(() => _catalogManager.DeleteWarehouse(warehouse.Id));
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

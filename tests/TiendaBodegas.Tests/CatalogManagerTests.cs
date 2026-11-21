using System.IO;
using TiendaBodegas.Application.Services;
using TiendaBodegas.Domain.Enums;
using TiendaBodegas.Infrastructure.Persistence.Json;
using TiendaBodegas.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TiendaBodegas.Tests;

/// <summary>
/// Unit tests covering CRUD operations, SKU/code uniqueness, and referential deletion guards in <see cref="CatalogManager"/>.
/// </summary>
public class CatalogManagerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly CatalogManager _catalogManager;
    private readonly InventoryManager _inventoryManager;

    /// <summary>
    /// Initializes an isolated JSON context and application services for each test run.
    /// </summary>
    public CatalogManagerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "TiendaBodegasCatalogTests_" + Guid.NewGuid().ToString("N"));
        var context = new JsonDataContext(_tempDirectory);
        var supplierRepo = new SupplierRepository(context);
        var productRepo = new ProductRepository(context);
        var movementRepo = new StockMovementRepository(context);

        _catalogManager = new CatalogManager(productRepo, supplierRepo, movementRepo);
        _inventoryManager = new InventoryManager(movementRepo, productRepo, supplierRepo);
    }

    /// <summary>
    /// Verifies that suppliers and products can be created, updated in place, and deleted when unused.
    /// </summary>
    [Fact]
    public void CreateUpdateAndDelete_WhenEntitiesHaveNoDependents_PersistsChanges()
    {
        var supplier = _catalogManager.CreateSupplier(
            "  Distribuidora Norte  ",
            " contacto@norte.com ",
            " Alimentos ",
            SupplierStatus.Active);

        var product = _catalogManager.CreateProduct(
            "  Café Molido  ",
            " CAF-001 ",
            " Alimentos ",
            supplier.Id,
            4.50m,
            7.25m,
            minStock: 8);

        _catalogManager.UpdateSupplier(
            supplier.Id,
            "Distribuidora Norte S.A.",
            "ventas@norte.com",
            "Bebidas",
            SupplierStatus.UnderReview,
            "PRV-99");

        _catalogManager.UpdateProduct(
            product.Id,
            "Café Premium",
            "CAF-001",
            "Bebidas",
            supplier.Id,
            5.00m,
            8.50m,
            minStock: 15);

        var reloadedContext = new JsonDataContext(_tempDirectory);
        reloadedContext.LoadJson();

        var savedSupplier = Assert.Single(reloadedContext.Suppliers);
        Assert.Equal("PRV-99", savedSupplier.Code);
        Assert.Equal("Distribuidora Norte S.A.", savedSupplier.Name);
        Assert.Equal("ventas@norte.com", savedSupplier.Contact);
        Assert.Equal("Bebidas", savedSupplier.MainCategory);
        Assert.Equal(SupplierStatus.UnderReview, savedSupplier.Status);

        var savedProduct = Assert.Single(reloadedContext.Products);
        Assert.Equal("Café Premium", savedProduct.Name);
        Assert.Equal("CAF-001", savedProduct.SKU);
        Assert.Equal("Bebidas", savedProduct.Category);
        Assert.Equal(supplier.Id, savedProduct.SupplierId);
        Assert.Equal(5.00m, savedProduct.PurchasePrice);
        Assert.Equal(8.50m, savedProduct.SalePrice);
        Assert.Equal(15, savedProduct.MinStock);

        _catalogManager.DeleteProduct(product.Id);
        _catalogManager.DeleteSupplier(supplier.Id);

        Assert.Empty(_catalogManager.GetAllProducts());
        Assert.Empty(_catalogManager.GetAllSuppliers());
    }

    /// <summary>
    /// Verifies that duplicate SKUs and supplier codes are rejected case-insensitively while allowing edits to keep their own keys.
    /// </summary>
    [Fact]
    public void CreateOrUpdate_WhenSkuOrSupplierCodeAlreadyExists_ThrowsInvalidOperationException()
    {
        var sup1 = _catalogManager.CreateSupplier("Prov A", "a@test.com", "Alimentos", SupplierStatus.Active, "PRV-10");
        var sup2 = _catalogManager.CreateSupplier("Prov B", "b@test.com", "Limpieza", SupplierStatus.Active, "PRV-20");

        Assert.Throws<InvalidOperationException>(() =>
            _catalogManager.CreateSupplier("Prov C", "c@test.com", "Hogar", SupplierStatus.Active, "prv-10"));

        Assert.Throws<InvalidOperationException>(() =>
            _catalogManager.UpdateSupplier(sup2.Id, "Prov B", "b@test.com", "Limpieza", SupplierStatus.Active, "prv-10"));

        _catalogManager.UpdateSupplier(sup1.Id, "Prov A Editado", "a@test.com", "Alimentos", SupplierStatus.Active, "PRV-10");

        var first = _catalogManager.CreateProduct("Producto A", "SKU-100", "Alimentos", sup1.Id, 6m, 10m);
        var second = _catalogManager.CreateProduct("Producto B", "SKU-200", "Limpieza", sup2.Id, 12m, 20m);

        Assert.Throws<InvalidOperationException>(() =>
            _catalogManager.CreateProduct("Producto C", "sku-100", "Alimentos", sup1.Id, 9m, 15m));

        Assert.Throws<InvalidOperationException>(() =>
            _catalogManager.UpdateProduct(second.Id, "Producto B", "sku-100", "Limpieza", sup2.Id, 12m, 20m));

        _catalogManager.UpdateProduct(first.Id, "Producto A Editado", "SKU-100", "Alimentos", sup1.Id, 7m, 12m);
        Assert.Equal(12m, first.SalePrice);
    }

    /// <summary>
    /// Verifies that products and suppliers with invalid inputs are rejected.
    /// </summary>
    [Fact]
    public void CreateProductOrSupplier_WithInvalidInputs_ThrowsValidationExceptions()
    {
        Assert.Throws<ArgumentException>(() => _catalogManager.CreateProduct("", "SKU-1", "Alimentos", null, 5m, 10m));
        Assert.Throws<ArgumentException>(() => _catalogManager.CreateProduct("Producto", " ", "Alimentos", null, 5m, 10m));
        Assert.Throws<ArgumentException>(() => _catalogManager.CreateProduct("Producto", "SKU-1", " ", null, 5m, 10m));
        Assert.Throws<ArgumentOutOfRangeException>(() => _catalogManager.CreateProduct("Producto", "SKU-1", "Alimentos", null, -1m, 10m));
        Assert.Throws<ArgumentOutOfRangeException>(() => _catalogManager.CreateProduct("Producto", "SKU-1", "Alimentos", null, 5m, 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => _catalogManager.CreateProduct("Producto", "SKU-1", "Alimentos", null, 5m, -3m));
        Assert.Throws<ArgumentOutOfRangeException>(() => _catalogManager.CreateProduct("Producto", "SKU-1", "Alimentos", null, 3m, 5m, minStock: -1));
        Assert.Throws<ArgumentException>(() => _catalogManager.CreateProduct("Producto", "SKU-1", "Alimentos", 999, 3m, 5m));

        Assert.Throws<ArgumentException>(() => _catalogManager.CreateSupplier("", "contacto@test.com", "Alimentos"));
        Assert.Throws<ArgumentException>(() => _catalogManager.CreateSupplier("Proveedor", " ", "Alimentos"));
        Assert.Throws<ArgumentException>(() => _catalogManager.CreateSupplier("Proveedor", "contacto@test.com", ""));
        Assert.Throws<ArgumentOutOfRangeException>(() => _catalogManager.CreateSupplier("Proveedor", "contacto@test.com", "Alimentos", (SupplierStatus)99));
    }

    /// <summary>
    /// Verifies that deleting a product with movements or a supplier with linked products is blocked.
    /// </summary>
    [Fact]
    public void DeleteProductOrSupplier_WhenDependenciesExist_ThrowsInvalidOperationException()
    {
        var supplier = _catalogManager.CreateSupplier("Proveedor Central", "ventas@central.com", "Alimentos");
        var usedProduct = _catalogManager.CreateProduct("Harina 1kg", "HAR-001", "Alimentos", supplier.Id, 1.20m, 2.00m);
        var unusedProduct = _catalogManager.CreateProduct("Sal 1kg", "SAL-001", "Alimentos", supplier.Id, 0.50m, 1.10m);

        _inventoryManager.RegisterEntry(usedProduct.Id, 15, "Stock inicial");

        var productEx = Assert.Throws<InvalidOperationException>(() => _catalogManager.DeleteProduct(usedProduct.Id));
        Assert.Contains("movimientos", productEx.Message);

        var supplierEx = Assert.Throws<InvalidOperationException>(() => _catalogManager.DeleteSupplier(supplier.Id));
        Assert.Contains("productos asociados", supplierEx.Message);

        _catalogManager.DeleteProduct(unusedProduct.Id);
        Assert.Throws<InvalidOperationException>(() => _catalogManager.DeleteSupplier(supplier.Id));
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

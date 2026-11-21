using System.IO;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Services;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Enums;
using TiendaBodegas.Infrastructure.Persistence.Json;
using TiendaBodegas.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TiendaBodegas.Tests;

/// <summary>
/// Unit tests covering single-warehouse stock calculations and business validation rules in <see cref="InventoryManager"/>.
/// </summary>
public class InventoryManagerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly InventoryManager _inventoryManager;
    private readonly ProductRepository _productRepository;
    private readonly Supplier _supplier;
    private readonly Product _product;

    /// <summary>
    /// Initializes an isolated data context, seed entities, and <see cref="InventoryManager"/> for each test.
    /// </summary>
    public InventoryManagerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "TiendaBodegasTests_" + Guid.NewGuid().ToString("N"));
        var context = new JsonDataContext(_tempDirectory);
        var supplierRepo = new SupplierRepository(context);
        _productRepository = new ProductRepository(context);
        var movementRepo = new StockMovementRepository(context);

        _supplier = new Supplier("Distribuidora Norte", "contacto@norte.com", "Alimentos", SupplierStatus.Active);
        supplierRepo.Add(_supplier);

        _product = new Product("Aceite 1L", "ACE-001", "Alimentos", _supplier.Id, 16.00m, 25.00m, minStock: 10);
        _productRepository.Add(_product);

        _inventoryManager = new InventoryManager(movementRepo, _productRepository, supplierRepo);
    }

    /// <summary>
    /// Verifies that entry and exit movements calculate global product stock accurately.
    /// </summary>
    [Fact]
    public void EntryAndExit_CalculatesGlobalStockCorrectly()
    {
        _inventoryManager.RegisterEntry(_product.Id, 50, "Ingreso inicial");
        _inventoryManager.RegisterExit(_product.Id, 10, "Venta mostrador");

        Assert.Equal(40, _inventoryManager.GetTotalStockByProduct(_product.Id));
    }

    /// <summary>
    /// Verifies that registering an exit exceeding available stock is rejected with a Spanish message.
    /// </summary>
    [Fact]
    public void RegisterExit_WhenInsufficientStock_ThrowsInvalidOperationException()
    {
        _inventoryManager.RegisterEntry(_product.Id, 5, "Stock inicial");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _inventoryManager.RegisterExit(_product.Id, 10, "Salida mayor al stock"));
        Assert.Contains("Stock insuficiente", ex.Message);
    }

    /// <summary>
    /// Verifies that zero or negative quantities are rejected across entry and exit operations.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Movements_WhenQuantityIsNotPositive_ThrowArgumentOutOfRangeException(int invalidQuantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _inventoryManager.RegisterEntry(_product.Id, invalidQuantity, "Cantidad inválida"));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _inventoryManager.RegisterExit(_product.Id, invalidQuantity, "Cantidad inválida"));
    }

    /// <summary>
    /// Verifies that movements referencing a non-existent product are rejected.
    /// </summary>
    [Fact]
    public void Movements_WhenProductDoesNotExist_ThrowArgumentException()
    {
        const int missingId = 999;

        Assert.Throws<ArgumentException>(() =>
            _inventoryManager.RegisterEntry(missingId, 5, "Producto inexistente"));

        Assert.Throws<ArgumentException>(() =>
            _inventoryManager.RegisterExit(missingId, 5, "Producto inexistente"));
    }

    /// <summary>
    /// Verifies unified <see cref="StockMovementRequest"/> registration across Entry and Exit operations.
    /// </summary>
    [Fact]
    public void RegisterMovement_HandlesEntryAndExitRequests()
    {
        _inventoryManager.RegisterMovement(new StockMovementRequest(
            _product.Id, MovementType.Entry, 50, "Entrada unificada"));
        _inventoryManager.RegisterMovement(new StockMovementRequest(
            _product.Id, MovementType.Exit, 12, "Salida unificada"));

        Assert.Equal(38, _inventoryManager.GetTotalStockByProduct(_product.Id));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _inventoryManager.RegisterMovement(new StockMovementRequest(_product.Id, (MovementType)99, 5, "Inválido")));
    }

    /// <summary>
    /// Verifies filtering of movement history by date range, movement type, and product.
    /// </summary>
    [Fact]
    public void GetMovements_AppliesFiltersAndOrdersDeterministicallyNewestFirst()
    {
        var secondProduct = new Product("Azúcar 1kg", "AZU-001", "Alimentos", _supplier.Id, 6m, 10m, minStock: 5);
        _productRepository.Add(secondProduct);

        _inventoryManager.RegisterEntry(_product.Id, 30, "M1");
        _inventoryManager.RegisterExit(_product.Id, 5, "M2");
        _inventoryManager.RegisterEntry(secondProduct.Id, 40, "M3");

        var allOrdered = _inventoryManager.GetMovements().ToList();
        Assert.Equal(new[] { "M3", "M2", "M1" }, allOrdered.Select(m => m.Notes).ToArray());

        var exits = _inventoryManager.GetMovements(new StockMovementFilter(MovementType: MovementType.Exit)).ToList();
        Assert.Single(exits);
        Assert.Equal("M2", exits[0].Notes);

        var secondProductMovements = _inventoryManager.GetMovements(new StockMovementFilter(ProductId: secondProduct.Id)).ToList();
        Assert.Single(secondProductMovements);
        Assert.Equal("M3", secondProductMovements[0].Notes);

        var todayOnly = _inventoryManager.GetMovements(new StockMovementFilter(
            FromDate: DateTime.Today,
            ToDate: DateTime.Today)).ToList();
        Assert.Equal(3, todayOnly.Count);
    }

    /// <summary>
    /// Verifies enriched movement valuation and global product stock summary status labels including out-of-stock products.
    /// </summary>
    [Fact]
    public void GetMovementDetailsAndProductStockSummary_ComputeValuationSupplierAndStatuses()
    {
        var lowProduct = new Product("Té Verde 250g", "TEA-001", "Bebidas", _supplier.Id, 4.10m, 6.90m, minStock: 10);
        var zeroStockProduct = new Product("Sal 1kg", "SAL-001", "Alimentos", null, 0.70m, 1.10m, minStock: 5);
        _productRepository.Add(lowProduct);
        _productRepository.Add(zeroStockProduct);

        _inventoryManager.RegisterEntry(_product.Id, 40, "Compra");
        _inventoryManager.RegisterExit(_product.Id, 10, "Venta");
        _inventoryManager.RegisterEntry(lowProduct.Id, 8, "Compra té");

        var details = _inventoryManager.GetMovementDetails().ToList();
        Assert.Equal(3, details.Count);

        var exitDetail = details[1];
        Assert.Equal("Salida", exitDetail.MovementTypeLabel);
        Assert.Equal(25.00m, exitDetail.UnitPrice);
        Assert.Equal(250.00m, exitDetail.TotalAmount);
        Assert.Equal($"#{_product.Id} — Aceite 1L", exitDetail.ProductDisplay);

        var entryDetail = details[2];
        Assert.Equal("Entrada", entryDetail.MovementTypeLabel);
        Assert.Equal(16.00m, entryDetail.UnitPrice);
        Assert.Equal(640.00m, entryDetail.TotalAmount);

        var summary = _inventoryManager.GetProductStockSummary().ToList();
        Assert.Equal(3, summary.Count);

        var oilSummary = Assert.Single(summary, s => s.ProductId == _product.Id);
        Assert.Equal(30, oilSummary.TotalStock);
        Assert.False(oilSummary.IsOutOfStock);
        Assert.False(oilSummary.IsLowStock);
        Assert.Equal("En Stock", oilSummary.StockStatusLabel);
        Assert.Equal("Distribuidora Norte", oilSummary.SupplierName);

        var teaSummary = Assert.Single(summary, s => s.ProductId == lowProduct.Id);
        Assert.Equal(8, teaSummary.TotalStock);
        Assert.False(teaSummary.IsOutOfStock);
        Assert.True(teaSummary.IsLowStock);
        Assert.Equal("Bajo Stock", teaSummary.StockStatusLabel);

        var saltSummary = Assert.Single(summary, s => s.ProductId == zeroStockProduct.Id);
        Assert.Equal(0, saltSummary.TotalStock);
        Assert.True(saltSummary.IsOutOfStock);
        Assert.True(saltSummary.IsLowStock);
        Assert.Equal("Sin Stock", saltSummary.StockStatusLabel);
        Assert.Equal("Sin proveedor", saltSummary.SupplierName);
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

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
/// Unit tests covering stock calculations and business validation rules in <see cref="InventoryManager"/>.
/// </summary>
public class InventoryManagerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly InventoryManager _inventoryManager;
    private readonly ProductRepository _productRepository;
    private readonly Warehouse _warehouse1;
    private readonly Warehouse _warehouse2;
    private readonly Product _product;

    /// <summary>
    /// Initializes an isolated data context, seed entities, and <see cref="InventoryManager"/> for each test.
    /// </summary>
    public InventoryManagerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "TiendaBodegasTests_" + Guid.NewGuid().ToString("N"));
        var context = new JsonDataContext(_tempDirectory);
        var warehouseRepo = new WarehouseRepository(context);
        _productRepository = new ProductRepository(context);
        var movementRepo = new StockMovementRepository(context);

        _warehouse1 = new Warehouse("Bodega Norte", "Calle 1");
        _warehouse2 = new Warehouse("Bodega Sur", "Calle 2");
        warehouseRepo.Add(_warehouse1);
        warehouseRepo.Add(_warehouse2);

        _product = new Product("Aceite 1L", "ACE-001", 25.00m, minStock: 25);
        _productRepository.Add(_product);

        _inventoryManager = new InventoryManager(movementRepo, warehouseRepo, _productRepository);
    }

    /// <summary>
    /// Verifies that entry, exit, and transfer update per-warehouse stock and global product stock accurately.
    /// </summary>
    [Fact]
    public void EntryExitAndTransfer_CalculatesStockByWarehouseAndTotalCorrectly()
    {
        _inventoryManager.RegisterEntry(_product.Id, _warehouse1.Id, 50, "Ingreso inicial");
        _inventoryManager.RegisterExit(_product.Id, _warehouse1.Id, 10, "Venta mostrador");
        _inventoryManager.TransferStock(_product.Id, _warehouse1.Id, _warehouse2.Id, 20, "Traslado a sur");

        var warehouse1Stock = Assert.Single(_inventoryManager.GetStockByWarehouse(_warehouse1.Id));
        var warehouse2Stock = Assert.Single(_inventoryManager.GetStockByWarehouse(_warehouse2.Id));

        Assert.Equal(20, warehouse1Stock.Quantity);
        Assert.Equal(20, warehouse2Stock.Quantity);
        Assert.Equal(40, _inventoryManager.GetTotalStockByProduct(_product.Id));
    }

    /// <summary>
    /// Verifies that transferring more stock than available in the source warehouse fails with a Spanish business message.
    /// </summary>
    [Fact]
    public void TransferStock_WhenInsufficientStock_ThrowsInvalidOperationException()
    {
        _inventoryManager.RegisterEntry(_product.Id, _warehouse1.Id, 10, "Stock inicial");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _inventoryManager.TransferStock(_product.Id, _warehouse1.Id, _warehouse2.Id, 15, "Excede stock"));
        Assert.Contains("Stock insuficiente en la bodega de origen", ex.Message);
    }

    /// <summary>
    /// Verifies that transferring stock between the same source and destination warehouse is rejected with a Spanish message.
    /// </summary>
    [Fact]
    public void TransferStock_WhenSourceAndDestinationAreEqual_ThrowsArgumentException()
    {
        _inventoryManager.RegisterEntry(_product.Id, _warehouse1.Id, 10, "Stock inicial");

        var ex = Assert.Throws<ArgumentException>(() =>
            _inventoryManager.TransferStock(_product.Id, _warehouse1.Id, _warehouse1.Id, 5, "Misma bodega"));
        Assert.Equal("La bodega de origen y la de destino deben ser diferentes.", ex.Message);
    }

    /// <summary>
    /// Verifies that registering an exit exceeding available warehouse stock is rejected with a Spanish message.
    /// </summary>
    [Fact]
    public void RegisterExit_WhenInsufficientStock_ThrowsInvalidOperationException()
    {
        _inventoryManager.RegisterEntry(_product.Id, _warehouse1.Id, 5, "Stock inicial");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _inventoryManager.RegisterExit(_product.Id, _warehouse1.Id, 10, "Salida mayor al stock"));
        Assert.Contains("Stock insuficiente en la bodega", ex.Message);
    }

    /// <summary>
    /// Verifies that zero or negative quantities are rejected across all movement operations.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Movements_WhenQuantityIsNotPositive_ThrowArgumentOutOfRangeException(int invalidQuantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _inventoryManager.RegisterEntry(_product.Id, _warehouse1.Id, invalidQuantity, "Cantidad inválida"));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _inventoryManager.RegisterExit(_product.Id, _warehouse1.Id, invalidQuantity, "Cantidad inválida"));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _inventoryManager.TransferStock(_product.Id, _warehouse1.Id, _warehouse2.Id, invalidQuantity, "Cantidad inválida"));
    }

    /// <summary>
    /// Verifies that movements referencing a non-existent product or warehouse are rejected.
    /// </summary>
    [Fact]
    public void Movements_WhenWarehouseOrProductDoesNotExist_ThrowArgumentException()
    {
        const int missingId = 999;

        Assert.Throws<ArgumentException>(() =>
            _inventoryManager.RegisterEntry(missingId, _warehouse1.Id, 5, "Producto inexistente"));

        Assert.Throws<ArgumentException>(() =>
            _inventoryManager.RegisterEntry(_product.Id, missingId, 5, "Bodega inexistente"));

        Assert.Throws<ArgumentException>(() =>
            _inventoryManager.RegisterExit(_product.Id, missingId, 5, "Bodega inexistente"));

        Assert.Throws<ArgumentException>(() =>
            _inventoryManager.TransferStock(missingId, _warehouse1.Id, _warehouse2.Id, 5, "Producto inexistente"));
    }

    /// <summary>
    /// Verifies unified <see cref="StockMovementRequest"/> registration across Entry, Exit, and Transfer operations.
    /// </summary>
    [Fact]
    public void RegisterMovement_HandlesEntryExitAndTransferRequests()
    {
        _inventoryManager.RegisterMovement(new StockMovementRequest(
            _product.Id, MovementType.Entry, null, _warehouse1.Id, 50, "Entrada unificada"));
        _inventoryManager.RegisterMovement(new StockMovementRequest(
            _product.Id, MovementType.Exit, _warehouse1.Id, null, 10, "Salida unificada"));
        _inventoryManager.RegisterMovement(new StockMovementRequest(
            _product.Id, MovementType.Transfer, _warehouse1.Id, _warehouse2.Id, 20, "Traslado unificado"));

        Assert.Equal(20, _inventoryManager.GetProductStockInWarehouse(_product.Id, _warehouse1.Id));
        Assert.Equal(20, _inventoryManager.GetProductStockInWarehouse(_product.Id, _warehouse2.Id));
        Assert.Equal(40, _inventoryManager.GetTotalStockByProduct(_product.Id));
    }

    /// <summary>
    /// Verifies filtering of movement history by date range, movement type, warehouse, and product.
    /// </summary>
    [Fact]
    public void GetMovements_AppliesFiltersAndOrdersDeterministicallyNewestFirst()
    {
        var secondProduct = new Product("Azúcar 1kg", "AZU-001", 10m, minStock: 5);
        _productRepository.Add(secondProduct);

        _inventoryManager.RegisterEntry(_product.Id, _warehouse1.Id, 30, "M1");
        _inventoryManager.RegisterExit(_product.Id, _warehouse1.Id, 5, "M2");
        _inventoryManager.RegisterEntry(secondProduct.Id, _warehouse2.Id, 40, "M3");
        _inventoryManager.TransferStock(_product.Id, _warehouse1.Id, _warehouse2.Id, 10, "M4");

        var allOrdered = _inventoryManager.GetMovements().ToList();
        Assert.Equal(new[] { "M4", "M3", "M2", "M1" }, allOrdered.Select(m => m.Notes).ToArray());

        var transfers = _inventoryManager.GetMovements(new StockMovementFilter(MovementType: MovementType.Transfer)).ToList();
        Assert.Single(transfers);
        Assert.Equal("M4", transfers[0].Notes);

        var warehouse2Movements = _inventoryManager.GetMovements(new StockMovementFilter(WarehouseId: _warehouse2.Id)).ToList();
        Assert.Equal(2, warehouse2Movements.Count);

        var secondProductMovements = _inventoryManager.GetMovements(new StockMovementFilter(ProductId: secondProduct.Id)).ToList();
        Assert.Single(secondProductMovements);
        Assert.Equal("M3", secondProductMovements[0].Notes);

        var todayOnly = _inventoryManager.GetMovements(new StockMovementFilter(
            FromDate: DateTime.Today,
            ToDate: DateTime.Today)).ToList();
        Assert.Equal(4, todayOnly.Count);
    }

    /// <summary>
    /// Verifies warehouse stock report generation, total stock calculation, and low-stock threshold detection.
    /// </summary>
    [Fact]
    public void GetWarehouseStockReport_ComputesLowStockFlagAgainstMinStock()
    {
        var healthyProduct = new Product("Arroz 1kg", "ARR-001", 5m, minStock: 10);
        _productRepository.Add(healthyProduct);

        _inventoryManager.RegisterEntry(_product.Id, _warehouse1.Id, 25, "Igual al mínimo");
        _inventoryManager.RegisterEntry(_product.Id, _warehouse2.Id, 30, "Sobre el mínimo");
        _inventoryManager.RegisterEntry(healthyProduct.Id, _warehouse1.Id, 50, "Stock sano");

        var report = _inventoryManager.GetWarehouseStockReport().ToList();
        Assert.Equal(3, report.Count);

        var w1Low = Assert.Single(report, r => r.WarehouseId == _warehouse1.Id && r.ProductId == _product.Id);
        Assert.Equal(25, w1Low.Quantity);
        Assert.Equal(55, w1Low.TotalProductStock);
        Assert.True(w1Low.IsLowStock);

        var w2Healthy = Assert.Single(report, r => r.WarehouseId == _warehouse2.Id && r.ProductId == _product.Id);
        Assert.Equal(30, w2Healthy.Quantity);
        Assert.False(w2Healthy.IsLowStock);

        var filteredW2 = _inventoryManager.GetWarehouseStockReport(_warehouse2.Id).ToList();
        Assert.Single(filteredW2);
    }

    /// <summary>
    /// Verifies enriched movement details with readable product/warehouse labels and global product stock summary including zero-movement products.
    /// </summary>
    [Fact]
    public void GetMovementDetailsAndProductStockSummary_ReturnReadableLabelsAndGlobalLowStockFlags()
    {
        var noMovementProduct = new Product("Sal 1kg", "SAL-001", 1.10m, minStock: 5);
        _productRepository.Add(noMovementProduct);

        _inventoryManager.RegisterEntry(_product.Id, _warehouse1.Id, 40, "Compra");
        _inventoryManager.RegisterExit(_product.Id, _warehouse1.Id, 10, "Venta");
        _inventoryManager.TransferStock(_product.Id, _warehouse1.Id, _warehouse2.Id, 15, "Traslado");

        var details = _inventoryManager.GetMovementDetails().ToList();
        Assert.Equal(3, details.Count);

        var transfer = details[0];
        Assert.Equal("Traslado", transfer.MovementTypeLabel);
        Assert.Equal($"#{_product.Id} — Aceite 1L", transfer.ProductDisplay);
        Assert.Equal($"#{_warehouse1.Id} — Bodega Norte", transfer.SourceWarehouseDisplay);
        Assert.Equal($"#{_warehouse2.Id} — Bodega Sur", transfer.DestinationWarehouseDisplay);

        var entry = details[2];
        Assert.Equal("Entrada", entry.MovementTypeLabel);
        Assert.Equal("—", entry.SourceWarehouseDisplay);
        Assert.Equal($"#{_warehouse1.Id} — Bodega Norte", entry.DestinationWarehouseDisplay);

        var summary = _inventoryManager.GetProductStockSummary().ToList();
        Assert.Equal(2, summary.Count);

        var oilSummary = Assert.Single(summary, s => s.ProductId == _product.Id);
        Assert.Equal(30, oilSummary.TotalStock);
        Assert.False(oilSummary.IsLowStock);

        var saltSummary = Assert.Single(summary, s => s.ProductId == noMovementProduct.Id);
        Assert.Equal(0, saltSummary.TotalStock);
        Assert.True(saltSummary.IsLowStock);
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

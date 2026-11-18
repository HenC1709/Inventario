using System.IO;
using TiendaBodegas.Application.Services;
using TiendaBodegas.Domain.Entities;
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
        var productRepo = new ProductRepository(context);
        var movementRepo = new StockMovementRepository(context);

        _warehouse1 = new Warehouse("Bodega Norte", "Calle 1");
        _warehouse2 = new Warehouse("Bodega Sur", "Calle 2");
        warehouseRepo.Add(_warehouse1);
        warehouseRepo.Add(_warehouse2);

        _product = new Product("Aceite 1L", "ACE-001", 25.00m);
        productRepo.Add(_product);

        _inventoryManager = new InventoryManager(movementRepo, warehouseRepo, productRepo);
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
    /// Verifies that transferring more stock than available in the source warehouse fails.
    /// </summary>
    [Fact]
    public void TransferStock_WhenInsufficientStock_ThrowsInvalidOperationException()
    {
        _inventoryManager.RegisterEntry(_product.Id, _warehouse1.Id, 10, "Stock inicial");

        Assert.Throws<InvalidOperationException>(() =>
            _inventoryManager.TransferStock(_product.Id, _warehouse1.Id, _warehouse2.Id, 15, "Excede stock"));
    }

    /// <summary>
    /// Verifies that transferring stock between the same source and destination warehouse is rejected.
    /// </summary>
    [Fact]
    public void TransferStock_WhenSourceAndDestinationAreEqual_ThrowsArgumentException()
    {
        _inventoryManager.RegisterEntry(_product.Id, _warehouse1.Id, 10, "Stock inicial");

        Assert.Throws<ArgumentException>(() =>
            _inventoryManager.TransferStock(_product.Id, _warehouse1.Id, _warehouse1.Id, 5, "Misma bodega"));
    }

    /// <summary>
    /// Verifies that registering an exit exceeding available warehouse stock is rejected.
    /// </summary>
    [Fact]
    public void RegisterExit_WhenInsufficientStock_ThrowsInvalidOperationException()
    {
        _inventoryManager.RegisterEntry(_product.Id, _warehouse1.Id, 5, "Stock inicial");

        Assert.Throws<InvalidOperationException>(() =>
            _inventoryManager.RegisterExit(_product.Id, _warehouse1.Id, 10, "Salida mayor al stock"));
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
    /// Cleans up the temporary test directory after execution.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }
}

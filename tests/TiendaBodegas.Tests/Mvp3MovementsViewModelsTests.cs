using System.IO;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Application.Services;
using TiendaBodegas.Domain.Enums;
using TiendaBodegas.Infrastructure.Persistence.Json;
using TiendaBodegas.Infrastructure.Persistence.Repositories;
using TiendaBodegas.UI.Services;
using TiendaBodegas.UI.ViewModels;
using Xunit;

namespace TiendaBodegas.Tests;

/// <summary>
/// Unit tests covering MVP 3 movement registration forms, <see cref="INotifyDataErrorInfo"/> validation, and service decoupling.
/// </summary>
public class Mvp3MovementsViewModelsTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly CatalogManager _catalogManager;
    private readonly InventoryManager _inventoryManager;

    /// <summary>
    /// Initializes an isolated JSON persistence context and application services for each test run.
    /// </summary>
    public Mvp3MovementsViewModelsTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "TiendaBodegasMvp3Tests_" + Guid.NewGuid().ToString("N"));
        var context = new JsonDataContext(_tempDirectory);
        var warehouseRepo = new WarehouseRepository(context);
        var productRepo = new ProductRepository(context);
        var movementRepo = new StockMovementRepository(context);

        _catalogManager = new CatalogManager(productRepo, warehouseRepo, movementRepo);
        _inventoryManager = new InventoryManager(movementRepo, warehouseRepo, productRepo);
    }

    /// <summary>
    /// Verifies that <see cref="MovementFormViewModel"/> validates required fields, positive integer quantities, and distinct transfer warehouses.
    /// </summary>
    [Fact]
    public void MovementFormViewModel_ValidatesFieldsWithINotifyDataErrorInfo()
    {
        var product = _catalogManager.CreateProduct("Arroz 1kg", "ARR-001", 2.00m, minStock: 10);
        var central = _catalogManager.CreateWarehouse("Bodega Central", "Av. 1");
        _catalogManager.CreateWarehouse("Bodega Sur", "Av. 2");

        var form = new MovementFormViewModel(_inventoryManager, _catalogManager, MovementType.Transfer);
        bool? closeResult = null;
        form.RequestClose += result => closeResult = result;

        form.QuantityText = "0";
        form.SaveCommand.Execute(null);

        Assert.True(form.HasErrors);
        Assert.NotEmpty(form.GetErrors(nameof(MovementFormViewModel.SelectedProduct)).Cast<string>());
        Assert.NotEmpty(form.GetErrors(nameof(MovementFormViewModel.SelectedSourceWarehouse)).Cast<string>());
        Assert.NotEmpty(form.GetErrors(nameof(MovementFormViewModel.SelectedDestinationWarehouse)).Cast<string>());
        Assert.NotEmpty(form.GetErrors(nameof(MovementFormViewModel.QuantityText)).Cast<string>());
        Assert.Null(closeResult);

        form.SelectedProduct = product;
        form.SelectedSourceWarehouse = central;
        form.SelectedDestinationWarehouse = central;
        form.QuantityText = "abc";

        Assert.NotEmpty(form.GetErrors(nameof(MovementFormViewModel.SelectedDestinationWarehouse)).Cast<string>());
        Assert.NotEmpty(form.GetErrors(nameof(MovementFormViewModel.QuantityText)).Cast<string>());

        form.QuantityText = "-3";
        Assert.NotEmpty(form.GetErrors(nameof(MovementFormViewModel.QuantityText)).Cast<string>());
    }

    /// <summary>
    /// Verifies that changing movement type updates visibility flags, title, and clears errors for hidden warehouse fields.
    /// </summary>
    [Fact]
    public void MovementFormViewModel_SwitchingMovementType_UpdatesRequirementsAndClearsIrrelevantErrors()
    {
        _catalogManager.CreateProduct("Aceite 1L", "ACE-001", 3.50m);
        _catalogManager.CreateWarehouse("Bodega Central", "Av. 1");

        var form = new MovementFormViewModel(_inventoryManager, _catalogManager, MovementType.Transfer);
        Assert.True(form.RequiresSourceWarehouse);
        Assert.True(form.RequiresDestinationWarehouse);
        Assert.Equal("Registrar Traslado", form.Title);

        form.SaveCommand.Execute(null);
        Assert.NotEmpty(form.GetErrors(nameof(MovementFormViewModel.SelectedSourceWarehouse)).Cast<string>());
        Assert.NotEmpty(form.GetErrors(nameof(MovementFormViewModel.SelectedDestinationWarehouse)).Cast<string>());

        form.SelectedMovementType = MovementType.Entry;
        Assert.False(form.RequiresSourceWarehouse);
        Assert.True(form.RequiresDestinationWarehouse);
        Assert.Equal("Registrar Entrada", form.Title);
        Assert.Empty(form.GetErrors(nameof(MovementFormViewModel.SelectedSourceWarehouse)).Cast<string>());

        form.SelectedMovementType = MovementType.Exit;
        Assert.True(form.RequiresSourceWarehouse);
        Assert.False(form.RequiresDestinationWarehouse);
        Assert.Equal("Registrar Salida", form.Title);
        Assert.Empty(form.GetErrors(nameof(MovementFormViewModel.SelectedDestinationWarehouse)).Cast<string>());
    }

    /// <summary>
    /// Verifies that business errors (insufficient stock) keep the modal open and populate <see cref="MovementFormViewModel.FormError"/> in Spanish.
    /// </summary>
    [Fact]
    public void MovementFormViewModel_WhenInsufficientStock_ShowsSpanishFormErrorWithoutClosing()
    {
        var product = _catalogManager.CreateProduct("Azúcar 1kg", "AZU-001", 1.80m);
        var central = _catalogManager.CreateWarehouse("Bodega Central", "Av. 1");
        var north = _catalogManager.CreateWarehouse("Bodega Norte", "Av. 2");
        _inventoryManager.RegisterEntry(product.Id, central.Id, 10, "Stock inicial");

        var form = new MovementFormViewModel(_inventoryManager, _catalogManager, MovementType.Transfer)
        {
            SelectedProduct = product,
            SelectedSourceWarehouse = central,
            SelectedDestinationWarehouse = north,
            QuantityText = "25",
            Notes = "Exceso"
        };

        bool? closeResult = null;
        form.RequestClose += result => closeResult = result;

        Assert.True(form.ShowAvailableStock);
        Assert.Equal(10, form.AvailableSourceStock);

        form.SaveCommand.Execute(null);

        Assert.Null(closeResult);
        Assert.Contains("Stock insuficiente", form.FormError);
    }

    /// <summary>
    /// Verifies valid Entry, Exit, and Transfer registrations through <see cref="MovementFormViewModel"/>.
    /// </summary>
    [Fact]
    public void MovementFormViewModel_ValidEntryExitAndTransfer_PersistAndCloseWithTrue()
    {
        var product = _catalogManager.CreateProduct("Leche 400g", "LEC-001", 1.25m);
        var central = _catalogManager.CreateWarehouse("Bodega Central", "Av. 1");
        var south = _catalogManager.CreateWarehouse("Bodega Sur", "Av. 2");

        var entryForm = new MovementFormViewModel(_inventoryManager, _catalogManager, MovementType.Entry)
        {
            SelectedProduct = product,
            SelectedDestinationWarehouse = central,
            QuantityText = "50",
            Notes = "Compra"
        };
        bool? entryClosed = null;
        entryForm.RequestClose += r => entryClosed = r;
        entryForm.SaveCommand.Execute(null);
        Assert.True(entryClosed);

        var exitForm = new MovementFormViewModel(_inventoryManager, _catalogManager, MovementType.Exit)
        {
            SelectedProduct = product,
            SelectedSourceWarehouse = central,
            QuantityText = "10",
            Notes = "Venta"
        };
        bool? exitClosed = null;
        exitForm.RequestClose += r => exitClosed = r;
        exitForm.SaveCommand.Execute(null);
        Assert.True(exitClosed);

        var transferForm = new MovementFormViewModel(_inventoryManager, _catalogManager, MovementType.Transfer)
        {
            SelectedProduct = product,
            SelectedSourceWarehouse = central,
            SelectedDestinationWarehouse = south,
            QuantityText = "20",
            Notes = "Traslado"
        };
        bool? transferClosed = null;
        transferForm.RequestClose += r => transferClosed = r;
        transferForm.SaveCommand.Execute(null);
        Assert.True(transferClosed);

        Assert.Equal(20, _inventoryManager.GetProductStockInWarehouse(product.Id, central.Id));
        Assert.Equal(20, _inventoryManager.GetProductStockInWarehouse(product.Id, south.Id));
        Assert.Equal(40, _inventoryManager.GetTotalStockByProduct(product.Id));
    }

    /// <summary>
    /// Verifies that <see cref="MovementsViewModel"/> depends solely on application services and <see cref="IDialogService"/>,
    /// validates catalog prerequisites, and reloads movements upon successful registration.
    /// </summary>
    [Fact]
    public void MovementsViewModel_ChecksPrerequisites_AndReloadsWhenMovementRegistered()
    {
        var constructorParams = typeof(MovementsViewModel).GetConstructors().Single().GetParameters();
        Assert.Equal(
            new[] { typeof(IInventoryService), typeof(ICatalogService), typeof(IDialogService) },
            constructorParams.Select(p => p.ParameterType).ToArray());

        var dialogStub = new StubMovementDialogService();
        var viewModel = new MovementsViewModel(_inventoryManager, _catalogManager, dialogStub);

        viewModel.RegisterEntryCommand.Execute(null);
        Assert.True(viewModel.IsErrorMessage);
        Assert.Contains("al menos un producto", viewModel.StatusMessage);

        var product = _catalogManager.CreateProduct("Arroz 1kg", "ARR-001", 1.85m);
        viewModel.RegisterEntryCommand.Execute(null);
        Assert.True(viewModel.IsErrorMessage);
        Assert.Contains("al menos una bodega", viewModel.StatusMessage);

        var central = _catalogManager.CreateWarehouse("Bodega Central", "Av. 1");
        viewModel.RegisterTransferCommand.Execute(null);
        Assert.True(viewModel.IsErrorMessage);
        Assert.Contains("al menos dos bodegas", viewModel.StatusMessage);

        dialogStub.OnShowMovementForm = form =>
        {
            form.SelectedProduct = product;
            form.SelectedDestinationWarehouse = central;
            form.QuantityText = "30";
            form.Notes = "Ingreso modal";
            form.SaveCommand.Execute(null);
            return true;
        };

        viewModel.RegisterEntryCommand.Execute(null);

        Assert.False(viewModel.IsErrorMessage);
        Assert.Contains("Entrada de inventario registrada", viewModel.StatusMessage);
        var loadedMovement = Assert.Single(viewModel.Movements);
        Assert.Equal(30, loadedMovement.Quantity);
        Assert.Equal("Ingreso modal", loadedMovement.Notes);
        Assert.Equal($"#{product.Id} — Arroz 1kg", loadedMovement.ProductDisplay);
    }

    /// <summary>
    /// Verifies that <see cref="StockViewModel"/> filters per-warehouse stock and global product totals and tracks low-stock alerts.
    /// </summary>
    [Fact]
    public void StockViewModel_FiltersByWarehouseSearchAndLowStock_AndComputesAlertCounts()
    {
        var rice = _catalogManager.CreateProduct("Arroz 1kg", "ARR-001", 2.00m, minStock: 30);
        var oil = _catalogManager.CreateProduct("Aceite 1L", "ACE-001", 4.00m, minStock: 10);
        var zeroStockProduct = _catalogManager.CreateProduct("Sal 1kg", "SAL-001", 1.00m, minStock: 5);

        var central = _catalogManager.CreateWarehouse("Bodega Central", "Av. 1");
        var north = _catalogManager.CreateWarehouse("Bodega Norte", "Av. 2");

        _inventoryManager.RegisterEntry(rice.Id, central.Id, 20, "Bajo mínimo");
        _inventoryManager.RegisterEntry(rice.Id, north.Id, 35, "Sobre mínimo");
        _inventoryManager.RegisterEntry(oil.Id, central.Id, 25, "Stock sano");

        var stockVm = new StockViewModel(_inventoryManager, _catalogManager);

        Assert.Equal(3, stockVm.StockItems.Count);
        Assert.Equal(3, stockVm.ProductTotals.Count);
        Assert.Equal(1, stockVm.LowStockCount);
        Assert.Equal(1, stockVm.LowTotalProductCount);
        Assert.True(stockVm.HasLowStockAlerts);

        stockVm.SelectedWarehouseFilter = stockVm.WarehouseFilters.First(w => w.WarehouseId == central.Id);
        Assert.Equal(2, stockVm.StockItems.Count);
        Assert.All(stockVm.StockItems, item => Assert.Equal(central.Id, item.WarehouseId));

        stockVm.ShowOnlyLowStock = true;
        var onlyLowWarehouse = Assert.Single(stockVm.StockItems);
        Assert.Equal(rice.Id, onlyLowWarehouse.ProductId);
        var onlyLowTotal = Assert.Single(stockVm.ProductTotals);
        Assert.Equal(zeroStockProduct.Id, onlyLowTotal.ProductId);

        stockVm.ClearFiltersCommand.Execute(null);
        Assert.Null(stockVm.SelectedWarehouseFilter?.WarehouseId);
        Assert.False(stockVm.ShowOnlyLowStock);
        Assert.Equal(3, stockVm.StockItems.Count);

        stockVm.SearchText = "ACE-001";
        Assert.Single(stockVm.StockItems);
        Assert.Single(stockVm.ProductTotals);
        Assert.False(stockVm.HasLowStockAlerts);
    }

    /// <summary>
    /// Verifies that <see cref="MovementsViewModel"/> filters history by type, warehouse, and date range, displays readable labels, and validates inverted dates.
    /// </summary>
    [Fact]
    public void MovementsViewModel_AppliesFiltersDisplaysNamesAndValidatesDateRange()
    {
        var rice = _catalogManager.CreateProduct("Arroz 1kg", "ARR-001", 2.00m);
        var central = _catalogManager.CreateWarehouse("Bodega Central", "Av. 1");
        var north = _catalogManager.CreateWarehouse("Bodega Norte", "Av. 2");

        _inventoryManager.RegisterEntry(rice.Id, central.Id, 50, "Compra inicial");
        _inventoryManager.RegisterExit(rice.Id, central.Id, 10, "Venta mostrador");
        _inventoryManager.TransferStock(rice.Id, central.Id, north.Id, 20, "Envío a norte");

        var movementsVm = new MovementsViewModel(_inventoryManager, _catalogManager, new StubMovementDialogService());

        Assert.Equal(3, movementsVm.Movements.Count);
        Assert.Equal("Traslado", movementsVm.Movements[0].MovementTypeLabel);
        Assert.Equal($"#{rice.Id} — Arroz 1kg", movementsVm.Movements[0].ProductDisplay);
        Assert.Equal($"#{central.Id} — Bodega Central", movementsVm.Movements[0].SourceWarehouseDisplay);
        Assert.Equal($"#{north.Id} — Bodega Norte", movementsVm.Movements[0].DestinationWarehouseDisplay);

        movementsVm.SelectedMovementTypeFilter = movementsVm.MovementTypeFilters.First(t => t.Type == MovementType.Exit);
        var exitOnly = Assert.Single(movementsVm.Movements);
        Assert.Equal("Salida", exitOnly.MovementTypeLabel);

        movementsVm.SelectedMovementTypeFilter = movementsVm.MovementTypeFilters[0];
        movementsVm.SelectedWarehouseFilter = movementsVm.WarehouseFilters.First(w => w.WarehouseId == north.Id);
        var northOnly = Assert.Single(movementsVm.Movements);
        Assert.Equal("Envío a norte", northOnly.Notes);

        movementsVm.FromDate = DateTime.Today.AddDays(1);
        movementsVm.ToDate = DateTime.Today;
        Assert.Empty(movementsVm.Movements);
        Assert.True(movementsVm.IsErrorMessage);
        Assert.Contains("posterior", movementsVm.StatusMessage);

        movementsVm.ClearFiltersCommand.Execute(null);
        Assert.False(movementsVm.IsErrorMessage);
        Assert.Equal(3, movementsVm.Movements.Count);
    }

    /// <summary>
    /// Cleans up the temporary test directory after execution.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    /// <summary>
    /// Test stub for <see cref="IDialogService"/> delegating movement modal interactions to a test callback.
    /// </summary>
    private sealed class StubMovementDialogService : IDialogService
    {
        /// <summary>
        /// Gets or sets the callback invoked when <see cref="ShowMovementForm"/> is called.
        /// </summary>
        public Func<MovementFormViewModel, bool>? OnShowMovementForm { get; set; }

        /// <summary>
        /// Returns false without displaying a modal window.
        /// </summary>
        public bool ShowProductForm(ProductFormViewModel viewModel) => false;

        /// <summary>
        /// Returns false without displaying a modal window.
        /// </summary>
        public bool ShowWarehouseForm(WarehouseFormViewModel viewModel) => false;

        /// <summary>
        /// Invokes <see cref="OnShowMovementForm"/> or returns false if unset.
        /// </summary>
        public bool ShowMovementForm(MovementFormViewModel viewModel) => OnShowMovementForm?.Invoke(viewModel) ?? false;

        /// <summary>
        /// Returns false without displaying a confirmation dialog.
        /// </summary>
        public bool Confirm(string message, string title) => false;
    }
}

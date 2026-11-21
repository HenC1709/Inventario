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
/// Unit tests covering single-warehouse movement registration forms, <see cref="INotifyDataErrorInfo"/> validation, and service decoupling.
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
        var supplierRepo = new SupplierRepository(context);
        var productRepo = new ProductRepository(context);
        var movementRepo = new StockMovementRepository(context);

        _catalogManager = new CatalogManager(productRepo, supplierRepo, movementRepo);
        _inventoryManager = new InventoryManager(movementRepo, productRepo, supplierRepo);
    }

    /// <summary>
    /// Verifies that <see cref="MovementFormViewModel"/> validates required product selection and strictly positive integer quantities.
    /// </summary>
    [Fact]
    public void MovementFormViewModel_ValidatesFieldsWithINotifyDataErrorInfo()
    {
        _catalogManager.CreateProduct("Arroz 1kg", "ARR-001", "Alimentos", null, 1.20m, 2.00m, minStock: 10);

        var form = new MovementFormViewModel(_inventoryManager, _catalogManager, MovementType.Entry);
        bool? closeResult = null;
        form.RequestClose += result => closeResult = result;

        form.QuantityText = "0";
        form.SaveCommand.Execute(null);

        Assert.True(form.HasErrors);
        Assert.NotEmpty(form.GetErrors(nameof(MovementFormViewModel.SelectedProduct)).Cast<string>());
        Assert.NotEmpty(form.GetErrors(nameof(MovementFormViewModel.QuantityText)).Cast<string>());
        Assert.Null(closeResult);

        form.QuantityText = "abc";
        Assert.NotEmpty(form.GetErrors(nameof(MovementFormViewModel.QuantityText)).Cast<string>());

        form.QuantityText = "-3";
        Assert.NotEmpty(form.GetErrors(nameof(MovementFormViewModel.QuantityText)).Cast<string>());
    }

    /// <summary>
    /// Verifies that changing movement type updates title and available stock indicator visibility.
    /// </summary>
    [Fact]
    public void MovementFormViewModel_SwitchingMovementType_UpdatesTitleAndAvailableStockIndicator()
    {
        var product = _catalogManager.CreateProduct("Aceite 1L", "ACE-001", "Alimentos", null, 2.00m, 3.50m);
        _inventoryManager.RegisterEntry(product.Id, 18, "Stock inicial");

        var form = new MovementFormViewModel(_inventoryManager, _catalogManager, MovementType.Entry)
        {
            SelectedProduct = product
        };
        Assert.False(form.ShowAvailableStock);
        Assert.Equal("Registrar Entrada", form.Title);

        form.SelectedMovementType = MovementType.Exit;
        Assert.True(form.ShowAvailableStock);
        Assert.Equal(18, form.AvailableStock);
        Assert.Equal("Registrar Salida", form.Title);
    }

    /// <summary>
    /// Verifies that business errors (insufficient stock) keep the modal open and populate <see cref="MovementFormViewModel.FormError"/> in Spanish.
    /// </summary>
    [Fact]
    public void MovementFormViewModel_WhenInsufficientStock_ShowsSpanishFormErrorWithoutClosing()
    {
        var product = _catalogManager.CreateProduct("Azúcar 1kg", "AZU-001", "Alimentos", null, 1.10m, 1.80m);
        _inventoryManager.RegisterEntry(product.Id, 10, "Stock inicial");

        var form = new MovementFormViewModel(_inventoryManager, _catalogManager, MovementType.Exit)
        {
            SelectedProduct = product,
            QuantityText = "25",
            Notes = "Exceso"
        };

        bool? closeResult = null;
        form.RequestClose += result => closeResult = result;

        Assert.True(form.ShowAvailableStock);
        Assert.Equal(10, form.AvailableStock);

        form.SaveCommand.Execute(null);

        Assert.Null(closeResult);
        Assert.Contains("Stock insuficiente", form.FormError);
    }

    /// <summary>
    /// Verifies valid Entry and Exit registrations through <see cref="MovementFormViewModel"/>.
    /// </summary>
    [Fact]
    public void MovementFormViewModel_ValidEntryAndExit_PersistAndCloseWithTrue()
    {
        var product = _catalogManager.CreateProduct("Leche 400g", "LEC-001", "Alimentos", null, 0.80m, 1.25m);

        var entryForm = new MovementFormViewModel(_inventoryManager, _catalogManager, MovementType.Entry)
        {
            SelectedProduct = product,
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
            QuantityText = "10",
            Notes = "Venta"
        };
        bool? exitClosed = null;
        exitForm.RequestClose += r => exitClosed = r;
        exitForm.SaveCommand.Execute(null);
        Assert.True(exitClosed);

        Assert.Equal(40, _inventoryManager.GetTotalStockByProduct(product.Id));
    }

    /// <summary>
    /// Verifies that <see cref="MovementsViewModel"/> depends solely on application services and <see cref="IDialogService"/>,
    /// validates product prerequisites, and reloads movements upon successful registration.
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

        var product = _catalogManager.CreateProduct("Arroz 1kg", "ARR-001", "Alimentos", null, 1.10m, 1.85m);

        dialogStub.OnShowMovementForm = form =>
        {
            form.SelectedProduct = product;
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
    /// Verifies that <see cref="StockViewModel"/> filters global product totals by search and low-stock toggle and computes alert counts.
    /// </summary>
    [Fact]
    public void StockViewModel_FiltersBySearchAndLowStock_AndComputesAlertCounts()
    {
        var supplier = _catalogManager.CreateSupplier("Distribuidora Norte", "contacto@norte.com", "Alimentos");
        var rice = _catalogManager.CreateProduct("Arroz 1kg", "ARR-001", "Alimentos", supplier.Id, 1.20m, 2.00m, minStock: 30);
        var oil = _catalogManager.CreateProduct("Aceite 1L", "ACE-001", "Alimentos", supplier.Id, 2.50m, 4.00m, minStock: 10);
        var zeroStockProduct = _catalogManager.CreateProduct("Sal 1kg", "SAL-001", "Condimentos", null, 0.50m, 1.00m, minStock: 5);

        _inventoryManager.RegisterEntry(rice.Id, 20, "Bajo mínimo");
        _inventoryManager.RegisterEntry(oil.Id, 25, "Stock sano");

        var stockVm = new StockViewModel(_inventoryManager);

        Assert.Equal(3, stockVm.ProductTotals.Count);
        Assert.Equal(2, stockVm.LowTotalProductCount);
        Assert.True(stockVm.HasLowStockAlerts);

        stockVm.ShowOnlyLowStock = true;
        Assert.Equal(2, stockVm.ProductTotals.Count);
        Assert.Contains(stockVm.ProductTotals, p => p.ProductId == rice.Id && p.StockStatusLabel == "Bajo Stock");
        Assert.Contains(stockVm.ProductTotals, p => p.ProductId == zeroStockProduct.Id && p.StockStatusLabel == "Sin Stock");

        stockVm.ClearFiltersCommand.Execute(null);
        Assert.False(stockVm.ShowOnlyLowStock);
        Assert.Equal(3, stockVm.ProductTotals.Count);

        stockVm.SearchText = "ACE-001";
        var onlyOil = Assert.Single(stockVm.ProductTotals);
        Assert.Equal(oil.Id, onlyOil.ProductId);
        Assert.False(stockVm.HasLowStockAlerts);
    }

    /// <summary>
    /// Verifies that <see cref="MovementsViewModel"/> filters history by type and date range, displays valuation, and validates inverted dates.
    /// </summary>
    [Fact]
    public void MovementsViewModel_AppliesFiltersDisplaysValuationAndValidatesDateRange()
    {
        var rice = _catalogManager.CreateProduct("Arroz 1kg", "ARR-001", "Alimentos", null, 1.20m, 2.00m);

        _inventoryManager.RegisterEntry(rice.Id, 50, "Compra inicial");
        _inventoryManager.RegisterExit(rice.Id, 10, "Venta mostrador");

        var movementsVm = new MovementsViewModel(_inventoryManager, _catalogManager, new StubMovementDialogService());

        Assert.Equal(2, movementsVm.Movements.Count);
        Assert.Equal("Salida", movementsVm.Movements[0].MovementTypeLabel);
        Assert.Equal(20.00m, movementsVm.Movements[0].TotalAmount);
        Assert.Equal($"#{rice.Id} — Arroz 1kg", movementsVm.Movements[0].ProductDisplay);

        movementsVm.SelectedMovementTypeFilter = movementsVm.MovementTypeFilters.First(t => t.Type == MovementType.Entry);
        var entryOnly = Assert.Single(movementsVm.Movements);
        Assert.Equal("Entrada", entryOnly.MovementTypeLabel);
        Assert.Equal(60.00m, entryOnly.TotalAmount);

        movementsVm.FromDate = DateTime.Today.AddDays(1);
        movementsVm.ToDate = DateTime.Today;
        Assert.Empty(movementsVm.Movements);
        Assert.True(movementsVm.IsErrorMessage);
        Assert.Contains("posterior", movementsVm.StatusMessage);

        movementsVm.ClearFiltersCommand.Execute(null);
        Assert.False(movementsVm.IsErrorMessage);
        Assert.Equal(2, movementsVm.Movements.Count);
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
        public bool ShowSupplierForm(SupplierFormViewModel viewModel) => false;

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

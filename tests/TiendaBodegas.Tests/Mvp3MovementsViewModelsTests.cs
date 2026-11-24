using System.IO;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Application.Services;
using TiendaBodegas.Domain.Enums;
using TiendaBodegas.Infrastructure.Persistence.Json;
using TiendaBodegas.Infrastructure.Persistence.Repositories;
using TiendaBodegas.UI.Converters;
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
            new[] { typeof(IInventoryService), typeof(ICatalogService), typeof(IDialogService), typeof(ISettingsService), typeof(IPrinterService) },
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
    /// Verifies that <see cref="InventoryViewModel"/> tracks alert counts and filters low/out-of-stock products accurately.
    /// </summary>
    [Fact]
    public void InventoryViewModel_FiltersAlertsAndSearch_AndComputesKpiCounters()
    {
        var supplier = _catalogManager.CreateSupplier("Distribuidora Norte", "contacto@norte.com", "Alimentos");
        var rice = _catalogManager.CreateProduct("Arroz 1kg", "ARR-001", "Alimentos", supplier.Id, 1.20m, 2.00m, minStock: 30);
        var oil = _catalogManager.CreateProduct("Aceite 1L", "ACE-001", "Alimentos", supplier.Id, 2.50m, 4.00m, minStock: 10);
        var zeroStockProduct = _catalogManager.CreateProduct("Sal 1kg", "SAL-001", "Condimentos", null, 0.50m, 1.00m, minStock: 5);

        _inventoryManager.RegisterEntry(rice.Id, 20, "Bajo mínimo");
        _inventoryManager.RegisterEntry(oil.Id, 25, "Stock sano");

        var inventoryVm = new InventoryViewModel(
            _catalogManager,
            _inventoryManager,
            new InventoryCsvExporter(),
            new StubMovementDialogService());

        Assert.Equal(3, inventoryVm.TotalProductsCount);
        Assert.Equal(2, inventoryVm.LowAndOutOfStockCount);

        inventoryVm.FilterLowStockCommand.Execute(null);
        Assert.Equal(2, inventoryVm.FilteredItemsCount);
        Assert.Contains(inventoryVm.PagedItems, p => p.ProductId == rice.Id && p.StockStatusLabel == "Bajo Stock");
        Assert.Contains(inventoryVm.PagedItems, p => p.ProductId == zeroStockProduct.Id && p.StockStatusLabel == "Sin Stock");

        inventoryVm.ResetFiltersCommand.Execute(null);
        Assert.Equal(3, inventoryVm.FilteredItemsCount);

        inventoryVm.SearchText = "ACE-001";
        var onlyOil = Assert.Single(inventoryVm.PagedItems);
        Assert.Equal(oil.Id, onlyOil.ProductId);
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
        Assert.True(movementsVm.HasNoMovements);
        Assert.True(movementsVm.IsErrorMessage);
        Assert.Contains("posterior", movementsVm.StatusMessage);

        movementsVm.ClearFiltersCommand.Execute(null);
        Assert.False(movementsVm.IsErrorMessage);
        Assert.False(movementsVm.HasNoMovements);
        Assert.Equal(2, movementsVm.Movements.Count);
    }

    /// <summary>
    /// Verifies <see cref="MovementTextFormatter"/> title, relative date, singular/plural units, and note formatting.
    /// </summary>
    [Fact]
    public void MovementTextFormatter_FormatsTitlesDatesUnitsAndNotesCorrectly()
    {
        var now = new DateTime(2026, 11, 21, 17, 30, 0);

        var exitToday = new StockMovementDisplayDto(
            7, now, MovementType.Exit, "Salida", 1, "SKU-1", "Café Espresso", "#1 — Café Espresso",
            5, 12.00m, 60.00m, "Orden #V-2041 — Salida de Stock");

        var entryYesterday = new StockMovementDisplayDto(
            2, now.AddDays(-1).Date.AddHours(9).AddMinutes(15), MovementType.Entry, "Entrada", 2, "SKU-2", "Aceite Oliva", "#2 — Aceite Oliva",
            1, 12.00m, 12.00m, "Reposición proveedor");

        var olderExit = new StockMovementDisplayDto(
            1, new DateTime(2026, 11, 10, 14, 5, 0), MovementType.Exit, "Salida", 3, "SKU-3", "Té Verde", "#3 — Té Verde",
            3, 6.90m, 20.70m, "");

        Assert.Equal("Venta #V-0007 — Salida de Stock", MovementTextFormatter.BuildTitle(exitToday));
        Assert.Equal("Compra #C-0002 — Entrada de Stock", MovementTextFormatter.BuildTitle(entryYesterday));

        Assert.Equal("Hoy, 05:30 PM • 5 unidades (Café Espresso)", MovementTextFormatter.BuildSubtitle(exitToday, now));
        Assert.Equal("Ayer, 09:15 AM • 1 unidad (Aceite Oliva) • Reposición proveedor", MovementTextFormatter.BuildSubtitle(entryYesterday, now));
        Assert.Equal("10/11/2026, 02:05 PM • 3 unidades (Té Verde)", MovementTextFormatter.BuildSubtitle(olderExit, now));
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
        public Func<MovementFormViewModel, bool>? OnShowMovementForm { get; set; }
        public bool ShowProductForm(ProductFormViewModel viewModel) => false;
        public bool ShowSupplierForm(SupplierFormViewModel viewModel) => false;
        public bool ShowMovementForm(MovementFormViewModel viewModel) => OnShowMovementForm?.Invoke(viewModel) ?? false;
        public bool Confirm(string message, string title) => false;
        public string? ShowSaveFileDialog(string defaultFileName, string filter) => null;
    }
}

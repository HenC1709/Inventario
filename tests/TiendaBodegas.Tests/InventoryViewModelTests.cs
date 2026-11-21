using System.IO;
using Microsoft.Extensions.DependencyInjection;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Application.Services;
using TiendaBodegas.Infrastructure.Persistence.Json;
using TiendaBodegas.Infrastructure.Persistence.Repositories;
using TiendaBodegas.UI;
using TiendaBodegas.UI.Services;
using TiendaBodegas.UI.ViewModels;
using Xunit;

namespace TiendaBodegas.Tests;

/// <summary>
/// Unit and integration tests covering unified <see cref="InventoryViewModel"/> KPIs, filtering, sorting, pagination, CSV export, and stock synchronization.
/// </summary>
public class InventoryViewModelTests : IDisposable
{
    private readonly string _tempDirectory;

    /// <summary>
    /// Initializes an isolated temporary directory for each test execution.
    /// </summary>
    public InventoryViewModelTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "TiendaBodegasInvTests_" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Verifies that KPI metrics match the prototype seed dataset (12 products, 5 alerts, 4 categories, $2,605.10).
    /// </summary>
    [Fact]
    public void KpiProperties_WithPrototypeSeedData_CalculateExpectedTotals()
    {
        using var provider = BuildSeededProvider();
        var vm = provider.GetRequiredService<InventoryViewModel>();

        Assert.Equal(12, vm.TotalProductsCount);
        Assert.Equal(5, vm.LowAndOutOfStockCount);
        Assert.Equal(4, vm.ActiveCategoriesCount);
        Assert.Equal(2605.10m, vm.EstimatedTotalValue);
        Assert.Equal("$2,605.10", vm.FormattedEstimatedValue);
        Assert.Equal("¡Atención!", vm.LowStockAlertBadgeText);
    }

    /// <summary>
    /// Verifies that KPI click commands filter alert items and reset all filters cleanly.
    /// </summary>
    [Fact]
    public void KpiCommands_FilterLowStockAndResetFilters_UpdateVisibleItems()
    {
        using var provider = BuildSeededProvider();
        var vm = provider.GetRequiredService<InventoryViewModel>();

        vm.FilterLowStockCommand.Execute(null);
        Assert.Equal(5, vm.FilteredItemsCount);
        Assert.Equal(1, vm.TotalPages);
        Assert.All(vm.PagedItems, item => Assert.True(item.IsLowStock || item.IsOutOfStock));

        vm.ResetFiltersCommand.Execute(null);
        Assert.Equal(12, vm.FilteredItemsCount);
        Assert.Equal(6, vm.PagedItems.Count);
        Assert.Equal(2, vm.TotalPages);
    }

    /// <summary>
    /// Verifies live search and Category, Status, and Supplier dropdown filters independently and combined.
    /// </summary>
    [Fact]
    public void SearchAndDropdownFilters_FilterByTextCategoryStatusAndSupplier()
    {
        using var provider = BuildSeededProvider();
        var vm = provider.GetRequiredService<InventoryViewModel>();

        vm.SearchText = "Espresso";
        var bySearch = Assert.Single(vm.PagedItems);
        Assert.Equal("SKU-10482", bySearch.ProductSku);

        vm.ResetFiltersCommand.Execute(null);
        vm.SelectedCategoryFilter = vm.CategoryFilters.First(c => c.Value == "Bebidas");
        Assert.Equal(2, vm.FilteredItemsCount);

        vm.SelectedStatusFilter = vm.StatusFilters.First(s => s.Value == InventoryViewModel.StatusInStock);
        var beverageInStock = Assert.Single(vm.PagedItems);
        Assert.Equal("SKU-77412", beverageInStock.ProductSku);

        vm.ResetFiltersCommand.Execute(null);
        vm.SelectedStatusFilter = vm.StatusFilters.First(s => s.Value == InventoryViewModel.StatusOutOfStock);
        Assert.Equal(2, vm.FilteredItemsCount);
        Assert.All(vm.PagedItems, item => Assert.Equal(0, item.TotalStock));

        vm.ResetFiltersCommand.Execute(null);
        vm.SelectedSupplierFilter = vm.SupplierFilters.First(s => s.Label.Contains("Hogar"));
        Assert.Equal(3, vm.FilteredItemsCount);
    }

    /// <summary>
    /// Verifies that sorting applies across the full filtered set before pagination slicing.
    /// </summary>
    [Fact]
    public void SortByCommand_SortsAcrossAllPagesAscendingAndDescending()
    {
        using var provider = BuildSeededProvider();
        var vm = provider.GetRequiredService<InventoryViewModel>();

        vm.SortByCommand.Execute(InventoryViewModel.SortSalePriceKey);
        Assert.Equal("↑", vm.SalePriceSortIndicator);
        Assert.Equal(2.50m, vm.PagedItems.First().SalePrice);

        vm.SortByCommand.Execute(InventoryViewModel.SortSalePriceKey);
        Assert.Equal("↓", vm.SalePriceSortIndicator);
        Assert.Equal(18.50m, vm.PagedItems.First().SalePrice);

        vm.SortByCommand.Execute(InventoryViewModel.SortStockKey);
        Assert.Equal("↑", vm.StockSortIndicator);
        Assert.Equal(0, vm.PagedItems.First().TotalStock);
    }

    /// <summary>
    /// Verifies page slicing, page indicator text, and forward/backward/jump pagination commands.
    /// </summary>
    [Fact]
    public void PaginationCommands_NavigatePagesAndUpdateInfoText()
    {
        using var provider = BuildSeededProvider();
        var vm = provider.GetRequiredService<InventoryViewModel>();

        Assert.Equal(1, vm.CurrentPage);
        Assert.Equal(2, vm.TotalPages);
        Assert.Equal("Mostrando 1-6 de 12 productos", vm.PaginationInfoText);
        Assert.False(vm.CanGoPrevious);
        Assert.True(vm.CanGoNext);

        vm.NextPageCommand.Execute(null);
        Assert.Equal(2, vm.CurrentPage);
        Assert.Equal(6, vm.PagedItems.Count);
        Assert.Equal("Mostrando 7-12 de 12 productos", vm.PaginationInfoText);
        Assert.True(vm.CanGoPrevious);
        Assert.False(vm.CanGoNext);

        vm.GoToPageCommand.Execute(1);
        Assert.Equal(1, vm.CurrentPage);

        vm.SearchText = "Inexistente-XYZ";
        Assert.True(vm.HasNoResults);
        Assert.Equal("Mostrando 0 de 0 productos", vm.PaginationInfoText);
    }

    /// <summary>
    /// Verifies row deletion error handling on products with movements and successful deletion on unused products.
    /// </summary>
    [Fact]
    public void DeleteProductByItemCommand_RespectsMovementGuardAndUpdatesCounts()
    {
        var context = new JsonDataContext(_tempDirectory);
        var supplierRepo = new SupplierRepository(context);
        var productRepo = new ProductRepository(context);
        var movementRepo = new StockMovementRepository(context);
        var catalog = new CatalogManager(productRepo, supplierRepo, movementRepo);
        var inventory = new InventoryManager(movementRepo, productRepo, supplierRepo);
        var exporter = new InventoryCsvExporter();
        var dialog = new StubInventoryDialogService { ConfirmResult = true };

        var usedProduct = catalog.CreateProduct("Arroz 1kg", "ARR-001", "Alimentos", null, 1.50m, 2.50m, 5);
        var unusedProduct = catalog.CreateProduct("Sal 1kg", "SAL-001", "Alimentos", null, 0.50m, 1.00m, 5);
        inventory.RegisterEntry(usedProduct.Id, 10, "Stock inicial");

        var vm = new InventoryViewModel(catalog, inventory, exporter, dialog);
        var observedCount = 0;
        vm.TotalProductCountChanged += count => observedCount = count;

        var usedSummary = vm.PagedItems.First(i => i.ProductId == usedProduct.Id);
        vm.DeleteProductByItemCommand.Execute(usedSummary);
        Assert.True(vm.IsErrorMessage);
        Assert.Contains("movimientos", vm.StatusMessage);
        Assert.Equal(2, vm.TotalProductsCount);

        var unusedSummary = vm.PagedItems.First(i => i.ProductId == unusedProduct.Id);
        vm.DeleteProductByItemCommand.Execute(unusedSummary);
        Assert.False(vm.IsErrorMessage);
        Assert.Equal(1, vm.TotalProductsCount);
        Assert.Equal(1, observedCount);
    }

    /// <summary>
    /// Verifies RFC-4180 CSV escaping and file generation from <see cref="InventoryViewModel.ExportCsvCommand"/>.
    /// </summary>
    [Fact]
    public void ExportCsvCommand_AndCsvExporter_GenerateEscapedCsvAndWriteFile()
    {
        var exporter = new InventoryCsvExporter();
        var items = new[]
        {
            new ProductStockSummaryDto(
                1,
                "SKU-01",
                "Café \"Especial\", 1kg",
                "Alimentos",
                null,
                "Proveedor, S.A.",
                5.50m,
                9.25m,
                10,
                15,
                false,
                false,
                "En Stock")
        };

        var csv = exporter.GenerateInventoryCsv(items);
        Assert.Contains("\"Café \"\"Especial\"\", 1kg\"", csv);
        Assert.Contains("\"Proveedor, S.A.\"", csv);
        Assert.Contains("5.50,9.25,15,10,En Stock", csv);

        var context = new JsonDataContext(_tempDirectory);
        var supplierRepo = new SupplierRepository(context);
        var productRepo = new ProductRepository(context);
        var movementRepo = new StockMovementRepository(context);
        var catalog = new CatalogManager(productRepo, supplierRepo, movementRepo);
        var inventory = new InventoryManager(movementRepo, productRepo, supplierRepo);
        var product = catalog.CreateProduct("Té Verde", "TEA-001", "Bebidas", null, 3.00m, 6.00m, 10);
        inventory.RegisterEntry(product.Id, 20, "Inicial");

        var exportPath = Path.Combine(_tempDirectory, "export.csv");
        var dialog = new StubInventoryDialogService { SaveFilePathResult = exportPath };
        var vm = new InventoryViewModel(catalog, inventory, exporter, dialog);

        vm.ExportCsvCommand.Execute(null);

        Assert.False(vm.IsErrorMessage);
        Assert.True(File.Exists(exportPath));
        var fileContent = File.ReadAllText(exportPath);
        Assert.Contains("TEA-001,Té Verde,Bebidas,Sin proveedor,3.00,6.00,20,10,En Stock", fileContent);
    }

    /// <summary>
    /// Verifies that <see cref="ProductFormViewModel"/> registers initial stock on create and delta adjustments on edit.
    /// </summary>
    [Fact]
    public void ProductFormViewModel_CreatesInitialStockAndAdjustsExistingStockViaMovements()
    {
        var context = new JsonDataContext(_tempDirectory);
        var supplierRepo = new SupplierRepository(context);
        var productRepo = new ProductRepository(context);
        var movementRepo = new StockMovementRepository(context);
        var catalog = new CatalogManager(productRepo, supplierRepo, movementRepo);
        var inventory = new InventoryManager(movementRepo, productRepo, supplierRepo);

        var createForm = new ProductFormViewModel(catalog, inventory)
        {
            Sku = "SKU-900",
            Category = "Alimentos",
            Name = "Granola 500g",
            PurchasePriceText = "2.50",
            SalePriceText = "4.50",
            StockText = "15",
            MinStockText = "5"
        };
        createForm.SaveCommand.Execute(null);

        var created = Assert.Single(catalog.GetAllProducts());
        Assert.Equal(15, inventory.GetTotalStockByProduct(created.Id));

        var increaseForm = new ProductFormViewModel(catalog, inventory, created)
        {
            StockText = "22"
        };
        increaseForm.SaveCommand.Execute(null);
        Assert.Equal(22, inventory.GetTotalStockByProduct(created.Id));

        var decreaseForm = new ProductFormViewModel(catalog, inventory, created)
        {
            StockText = "18"
        };
        decreaseForm.SaveCommand.Execute(null);
        Assert.Equal(18, inventory.GetTotalStockByProduct(created.Id));
        Assert.Equal(3, inventory.GetMovements().Count());
    }

    private ServiceProvider BuildSeededProvider()
    {
        var services = new ServiceCollection();
        services.AddTiendaBodegasServices(_tempDirectory);
        var provider = services.BuildServiceProvider();
        DependencyInjection.InitializeDatabase(provider);
        return provider;
    }

    /// <summary>
    /// Cleans up the temporary directory after each test.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    private sealed class StubInventoryDialogService : IDialogService
    {
        public bool ConfirmResult { get; set; }
        public string? SaveFilePathResult { get; set; }

        public bool ShowProductForm(ProductFormViewModel viewModel) => false;
        public bool ShowSupplierForm(SupplierFormViewModel viewModel) => false;
        public bool ShowMovementForm(MovementFormViewModel viewModel) => false;
        public bool Confirm(string message, string title) => ConfirmResult;
        public string? ShowSaveFileDialog(string defaultFileName, string filter) => SaveFilePathResult;
    }
}

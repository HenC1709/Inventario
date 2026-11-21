using System.IO;
using TiendaBodegas.Application.Services;
using TiendaBodegas.Infrastructure.Persistence.Json;
using TiendaBodegas.Infrastructure.Persistence.Repositories;
using TiendaBodegas.UI.Services;
using TiendaBodegas.UI.ViewModels;
using Xunit;

namespace TiendaBodegas.Tests;

/// <summary>
/// Unit tests covering MVP 2 <see cref="System.ComponentModel.INotifyDataErrorInfo"/> form validation, search filtering, and error reporting.
/// </summary>
public class Mvp2CrudViewModelsTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly CatalogManager _catalogManager;
    private readonly InventoryManager _inventoryManager;

    /// <summary>
    /// Initializes an isolated JSON persistence context and application services for each test run.
    /// </summary>
    public Mvp2CrudViewModelsTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "TiendaBodegasMvp2Tests_" + Guid.NewGuid().ToString("N"));
        var context = new JsonDataContext(_tempDirectory);
        var warehouseRepo = new WarehouseRepository(context);
        var productRepo = new ProductRepository(context);
        var movementRepo = new StockMovementRepository(context);

        _catalogManager = new CatalogManager(productRepo, warehouseRepo, movementRepo);
        _inventoryManager = new InventoryManager(movementRepo, warehouseRepo, productRepo);
    }

    /// <summary>
    /// Verifies that <see cref="ProductFormViewModel"/> keeps the modal open on invalid fields and saves when valid.
    /// </summary>
    [Fact]
    public void ProductFormViewModel_ValidatesWithINotifyDataErrorInfo_AndSavesOnlyWhenValid()
    {
        _catalogManager.CreateProduct("Existente", "SKU-EXIST", 10m);

        var form = new ProductFormViewModel(_catalogManager);
        bool? closeResult = null;
        form.RequestClose += result => closeResult = result;

        form.Name = "";
        form.Sku = "sku-exist";
        form.PriceText = "0";
        form.MinStockText = "-5";
        form.SaveCommand.Execute(null);

        Assert.True(form.HasErrors);
        Assert.NotEmpty(form.GetErrors(nameof(ProductFormViewModel.Name)).Cast<string>());
        Assert.NotEmpty(form.GetErrors(nameof(ProductFormViewModel.Sku)).Cast<string>());
        Assert.NotEmpty(form.GetErrors(nameof(ProductFormViewModel.PriceText)).Cast<string>());
        Assert.NotEmpty(form.GetErrors(nameof(ProductFormViewModel.MinStockText)).Cast<string>());
        Assert.Null(closeResult);

        form.Name = "Nuevo Producto";
        form.Sku = "SKU-NEW";
        form.PriceText = "15.50";
        form.MinStockText = "12";
        form.SaveCommand.Execute(null);

        Assert.False(form.HasErrors);
        Assert.True(closeResult);
        Assert.Contains(_catalogManager.GetAllProducts(), p => p.SKU == "SKU-NEW" && p.Price == 15.50m && p.MinStock == 12);
    }

    /// <summary>
    /// Verifies that <see cref="WarehouseFormViewModel"/> rejects empty fields and persists valid edits.
    /// </summary>
    [Fact]
    public void WarehouseFormViewModel_ValidatesWithINotifyDataErrorInfo_AndSavesOnlyWhenValid()
    {
        var form = new WarehouseFormViewModel(_catalogManager);
        bool? closeResult = null;
        form.RequestClose += result => closeResult = result;

        form.SaveCommand.Execute(null);

        Assert.True(form.HasErrors);
        Assert.NotEmpty(form.GetErrors(nameof(WarehouseFormViewModel.Name)).Cast<string>());
        Assert.NotEmpty(form.GetErrors(nameof(WarehouseFormViewModel.Address)).Cast<string>());
        Assert.Null(closeResult);

        form.Name = "Bodega Oeste";
        form.Address = "Av. Libertad 500";
        form.SaveCommand.Execute(null);

        Assert.False(form.HasErrors);
        Assert.True(closeResult);
        Assert.Contains(_catalogManager.GetAllWarehouses(), w => w.Name == "Bodega Oeste");
    }

    /// <summary>
    /// Verifies search filtering by name/SKU and non-crashing error feedback when deleting a product with movements.
    /// </summary>
    [Fact]
    public void ProductsViewModel_FiltersByNameOrSku_AndShowsErrorWhenDeletingUsedProduct()
    {
        var rice = _catalogManager.CreateProduct("Arroz Extra", "ARR-001", 2.50m);
        var oil = _catalogManager.CreateProduct("Aceite Vegetal", "ACE-002", 4.00m);
        var warehouse = _catalogManager.CreateWarehouse("Bodega Central", "Av. 1");
        _inventoryManager.RegisterEntry(rice.Id, warehouse.Id, 10, "Ingreso");

        var dialogStub = new StubDialogService { ConfirmResult = true };
        var viewModel = new ProductsViewModel(_catalogManager, dialogStub);

        viewModel.SearchText = "ACE-002";
        var filteredBySku = Assert.Single(viewModel.Products);
        Assert.Equal(oil.Id, filteredBySku.Id);

        viewModel.SearchText = "arroz";
        var filteredByName = Assert.Single(viewModel.Products);
        Assert.Equal(rice.Id, filteredByName.Id);

        viewModel.SelectedProduct = rice;
        viewModel.DeleteProductCommand.Execute(null);

        Assert.True(viewModel.IsErrorMessage);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.StatusMessage));
        Assert.Contains(_catalogManager.GetAllProducts(), p => p.Id == rice.Id);
    }

    /// <summary>
    /// Verifies search filtering by name/address and non-crashing error feedback when deleting a warehouse with movements.
    /// </summary>
    [Fact]
    public void WarehousesViewModel_FiltersByNameOrAddress_AndShowsErrorWhenDeletingUsedWarehouse()
    {
        var product = _catalogManager.CreateProduct("Azúcar 1kg", "AZU-001", 1.80m);
        var central = _catalogManager.CreateWarehouse("Bodega Central", "Av. Principal 10");
        var north = _catalogManager.CreateWarehouse("Bodega Norte", "Calle Pinos 20");
        _inventoryManager.RegisterEntry(product.Id, central.Id, 5, "Ingreso");

        var dialogStub = new StubDialogService { ConfirmResult = true };
        var viewModel = new WarehousesViewModel(_catalogManager, dialogStub);

        viewModel.SearchText = "Pinos";
        var filteredByAddress = Assert.Single(viewModel.Warehouses);
        Assert.Equal(north.Id, filteredByAddress.Id);

        viewModel.SearchText = "";
        viewModel.SelectedWarehouse = central;
        viewModel.DeleteWarehouseCommand.Execute(null);

        Assert.True(viewModel.IsErrorMessage);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.StatusMessage));
        Assert.Contains(_catalogManager.GetAllWarehouses(), w => w.Id == central.Id);
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
    /// Test stub for <see cref="IDialogService"/> returning deterministic outcomes without opening WPF windows.
    /// </summary>
    private sealed class StubDialogService : IDialogService
    {
        /// <summary>
        /// Gets or sets the result returned by <see cref="Confirm"/>.
        /// </summary>
        public bool ConfirmResult { get; set; }

        /// <summary>
        /// Returns false without displaying a modal window.
        /// </summary>
        public bool ShowProductForm(ProductFormViewModel viewModel) => false;

        /// <summary>
        /// Returns false without displaying a modal window.
        /// </summary>
        public bool ShowWarehouseForm(WarehouseFormViewModel viewModel) => false;

        /// <summary>
        /// Returns false without displaying a modal window.
        /// </summary>
        public bool ShowMovementForm(MovementFormViewModel viewModel) => false;

        /// <summary>
        /// Returns <see cref="ConfirmResult"/> without displaying a message box.
        /// </summary>
        public bool Confirm(string message, string title) => ConfirmResult;
    }
}

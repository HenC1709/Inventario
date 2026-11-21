using System.IO;
using TiendaBodegas.Application.Services;
using TiendaBodegas.Domain.Enums;
using TiendaBodegas.Infrastructure.Persistence.Json;
using TiendaBodegas.Infrastructure.Persistence.Repositories;
using TiendaBodegas.UI.Services;
using TiendaBodegas.UI.ViewModels;
using Xunit;

namespace TiendaBodegas.Tests;

/// <summary>
/// Unit tests covering CRUD <see cref="System.ComponentModel.INotifyDataErrorInfo"/> form validation, search filtering, and error reporting.
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
        var supplierRepo = new SupplierRepository(context);
        var productRepo = new ProductRepository(context);
        var movementRepo = new StockMovementRepository(context);

        _catalogManager = new CatalogManager(productRepo, supplierRepo, movementRepo);
        _inventoryManager = new InventoryManager(movementRepo, productRepo, supplierRepo);
    }

    /// <summary>
    /// Verifies that <see cref="ProductFormViewModel"/> keeps the modal open on invalid fields and saves when valid.
    /// </summary>
    [Fact]
    public void ProductFormViewModel_ValidatesWithINotifyDataErrorInfo_AndSavesOnlyWhenValid()
    {
        var supplier = _catalogManager.CreateSupplier("Distribuidora Norte", "contacto@norte.com", "Alimentos");
        _catalogManager.CreateProduct("Existente", "SKU-EXIST", "Alimentos", supplier.Id, 6m, 10m);

        var form = new ProductFormViewModel(_catalogManager, _inventoryManager);
        bool? closeResult = null;
        form.RequestClose += result => closeResult = result;

        form.Name = "";
        form.Sku = "sku-exist";
        form.Category = "";
        form.PurchasePriceText = "-1";
        form.SalePriceText = "0";
        form.StockText = "-2";
        form.MinStockText = "-5";
        form.SaveCommand.Execute(null);

        Assert.True(form.HasErrors);
        Assert.NotEmpty(form.GetErrors(nameof(ProductFormViewModel.Name)).Cast<string>());
        Assert.NotEmpty(form.GetErrors(nameof(ProductFormViewModel.Sku)).Cast<string>());
        Assert.NotEmpty(form.GetErrors(nameof(ProductFormViewModel.Category)).Cast<string>());
        Assert.NotEmpty(form.GetErrors(nameof(ProductFormViewModel.PurchasePriceText)).Cast<string>());
        Assert.NotEmpty(form.GetErrors(nameof(ProductFormViewModel.SalePriceText)).Cast<string>());
        Assert.NotEmpty(form.GetErrors(nameof(ProductFormViewModel.StockText)).Cast<string>());
        Assert.NotEmpty(form.GetErrors(nameof(ProductFormViewModel.MinStockText)).Cast<string>());
        Assert.Null(closeResult);

        form.Name = "Nuevo Producto";
        form.Sku = "SKU-NEW";
        form.Category = "Alimentos";
        form.SelectedSupplier = supplier;
        form.PurchasePriceText = "9.50";
        form.SalePriceText = "15.50";
        form.StockText = "20";
        form.MinStockText = "12";
        form.SaveCommand.Execute(null);

        Assert.False(form.HasErrors);
        Assert.True(closeResult);
        var saved = Assert.Single(_catalogManager.GetAllProducts(), p => p.SKU == "SKU-NEW");
        Assert.Equal("Alimentos", saved.Category);
        Assert.Equal(supplier.Id, saved.SupplierId);
        Assert.Equal(9.50m, saved.PurchasePrice);
        Assert.Equal(15.50m, saved.SalePrice);
        Assert.Equal(12, saved.MinStock);
        Assert.Equal(20, _inventoryManager.GetTotalStockByProduct(saved.Id));
    }

    /// <summary>
    /// Verifies that <see cref="SupplierFormViewModel"/> rejects empty or duplicate fields and persists valid edits.
    /// </summary>
    [Fact]
    public void SupplierFormViewModel_ValidatesWithINotifyDataErrorInfo_AndSavesOnlyWhenValid()
    {
        _catalogManager.CreateSupplier("Existente S.A.", "info@exist.com", "Hogar", SupplierStatus.Active, "PRV-01");

        var form = new SupplierFormViewModel(_catalogManager);
        bool? closeResult = null;
        form.RequestClose += result => closeResult = result;

        form.Code = "prv-01";
        form.SaveCommand.Execute(null);

        Assert.True(form.HasErrors);
        Assert.NotEmpty(form.GetErrors(nameof(SupplierFormViewModel.Code)).Cast<string>());
        Assert.NotEmpty(form.GetErrors(nameof(SupplierFormViewModel.Name)).Cast<string>());
        Assert.NotEmpty(form.GetErrors(nameof(SupplierFormViewModel.Contact)).Cast<string>());
        Assert.NotEmpty(form.GetErrors(nameof(SupplierFormViewModel.MainCategory)).Cast<string>());
        Assert.Null(closeResult);

        form.Code = "PRV-02";
        form.Name = "Químicos Global";
        form.Contact = "ventas@qlglobal.com";
        form.MainCategory = "Limpieza";
        form.SelectedStatus = SupplierStatus.UnderReview;
        form.SaveCommand.Execute(null);

        Assert.False(form.HasErrors);
        Assert.True(closeResult);
        Assert.Contains(_catalogManager.GetAllSuppliers(), s =>
            s.Code == "PRV-02"
            && s.Name == "Químicos Global"
            && s.Status == SupplierStatus.UnderReview);
    }

    /// <summary>
    /// Verifies search filtering by name/SKU/category and non-crashing error feedback when deleting a product with movements.
    /// </summary>
    [Fact]
    public void InventoryViewModel_FiltersByNameSkuOrCategory_AndShowsErrorWhenDeletingUsedProduct()
    {
        var supplier = _catalogManager.CreateSupplier("Distribuidora Norte", "contacto@norte.com", "Alimentos");
        var rice = _catalogManager.CreateProduct("Arroz Extra", "ARR-001", "Alimentos", supplier.Id, 1.50m, 2.50m);
        var detergent = _catalogManager.CreateProduct("Detergente 3L", "DET-002", "Limpieza", supplier.Id, 2.80m, 4.00m);
        _inventoryManager.RegisterEntry(rice.Id, 10, "Ingreso");

        var dialogStub = new StubDialogService { ConfirmResult = true };
        var viewModel = new InventoryViewModel(_catalogManager, _inventoryManager, new InventoryCsvExporter(), dialogStub);

        viewModel.SearchText = "DET-002";
        var filteredBySku = Assert.Single(viewModel.PagedItems);
        Assert.Equal(detergent.Id, filteredBySku.ProductId);

        viewModel.SearchText = "Limpieza";
        var filteredByCategory = Assert.Single(viewModel.PagedItems);
        Assert.Equal(detergent.Id, filteredByCategory.ProductId);

        viewModel.SearchText = "arroz";
        var filteredByName = Assert.Single(viewModel.PagedItems);
        Assert.Equal(rice.Id, filteredByName.ProductId);

        viewModel.DeleteProductByItemCommand.Execute(filteredByName);

        Assert.True(viewModel.IsErrorMessage);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.StatusMessage));
        Assert.Contains(_catalogManager.GetAllProducts(), p => p.Id == rice.Id);
    }

    /// <summary>
    /// Verifies search filtering by code/name/contact/category and non-crashing error feedback when deleting a supplier with products.
    /// </summary>
    [Fact]
    public void SuppliersViewModel_FiltersByFields_AndShowsErrorWhenDeletingUsedSupplier()
    {
        var north = _catalogManager.CreateSupplier("Distribuidora Norte", "contacto@norte.com", "Alimentos");
        var hogar = _catalogManager.CreateSupplier("Insumos Hogar", "pedidos@hogar.com", "Hogar");
        _catalogManager.CreateProduct("Azúcar 1kg", "AZU-001", "Alimentos", north.Id, 1.10m, 1.80m);

        var dialogStub = new StubDialogService { ConfirmResult = true };
        var viewModel = new SuppliersViewModel(_catalogManager, dialogStub);

        viewModel.SearchText = "pedidos@hogar.com";
        var filteredByContact = Assert.Single(viewModel.Suppliers);
        Assert.Equal(hogar.Id, filteredByContact.Id);

        viewModel.SearchText = "";
        viewModel.SelectedSupplier = north;
        viewModel.DeleteSupplierCommand.Execute(null);

        Assert.True(viewModel.IsErrorMessage);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.StatusMessage));
        Assert.Contains(_catalogManager.GetAllSuppliers(), s => s.Id == north.Id);
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
        public bool ConfirmResult { get; set; }
        public bool ShowProductForm(ProductFormViewModel viewModel) => false;
        public bool ShowSupplierForm(SupplierFormViewModel viewModel) => false;
        public bool ShowMovementForm(MovementFormViewModel viewModel) => false;
        public bool Confirm(string message, string title) => ConfirmResult;
        public string? ShowSaveFileDialog(string defaultFileName, string filter) => null;
    }
}

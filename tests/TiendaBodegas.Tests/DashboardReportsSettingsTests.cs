using System.IO;
using Microsoft.Extensions.DependencyInjection;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Application.Services;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Infrastructure.Persistence.Json;
using TiendaBodegas.Infrastructure.Persistence.Repositories;
using TiendaBodegas.UI;
using TiendaBodegas.UI.Services;
using TiendaBodegas.UI.ViewModels;
using Xunit;

namespace TiendaBodegas.Tests;

/// <summary>
/// Unit and integration tests covering R7 (Dashboard), R8 (Reports & CSV exports), and R9 (Settings persistence).
/// </summary>
public class DashboardReportsSettingsTests : IDisposable
{
    private readonly string _tempDirectory;

    /// <summary>
    /// Initializes an isolated temporary directory for each test execution.
    /// </summary>
    public DashboardReportsSettingsTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "TiendaBodegasR7R9Tests_" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Verifies that <see cref="DashboardViewModel"/> computes category percentages and critical alerts and navigates to filtered inventory.
    /// </summary>
    [Fact]
    public void DashboardViewModel_ComputesCategoryBarsAndAlerts_AndNavigatesToInventory()
    {
        using var provider = BuildSeededProvider();
        var mainVm = provider.GetRequiredService<MainViewModel>();
        var dashboardVm = provider.GetRequiredService<DashboardViewModel>();
        var inventoryVm = provider.GetRequiredService<InventoryViewModel>();

        mainVm.ShowDashboardCommand.Execute(null);

        Assert.IsType<DashboardViewModel>(mainVm.CurrentViewModel);
        Assert.Equal(4, dashboardVm.CategoryDistributions.Count);
        Assert.False(dashboardVm.HasNoCategories);
        Assert.False(dashboardVm.HasNoAlerts);
        Assert.Equal(5, dashboardVm.TotalAlertsCount);
        Assert.Equal(5, dashboardVm.CriticalAlerts.Count);

        var topCategory = dashboardVm.CategoryDistributions[0];
        Assert.Equal("Hogar", topCategory.Category);
        Assert.Equal(213, topCategory.TotalUnits);
        Assert.Equal(100.0, topCategory.Percentage);
        Assert.Equal("213 uds.", topCategory.UnitsLabel);

        Assert.Equal(0, dashboardVm.CriticalAlerts[0].TotalStock);

        dashboardVm.ReviewCriticalProductsCommand.Execute(null);
        Assert.IsType<InventoryViewModel>(mainVm.CurrentViewModel);
        Assert.True(mainVm.IsInventoryActive);
        Assert.Equal(5, inventoryVm.FilteredItemsCount);

        mainVm.ShowDashboardCommand.Execute(null);
        dashboardVm.GoToInventoryCommand.Execute(null);
        Assert.IsType<InventoryViewModel>(mainVm.CurrentViewModel);
        Assert.Equal(12, inventoryVm.TotalProductsCount);
    }

    /// <summary>
    /// Verifies category valuation aggregation and 30-day low-rotation product detection in <see cref="InventoryManager"/>.
    /// </summary>
    [Fact]
    public void InventoryManager_ComputesCategoryValuationAndLowRotationAudit()
    {
        Directory.CreateDirectory(_tempDirectory);
        var referenceDate = new DateTime(2026, 11, 21, 12, 0, 0);

        var context = new JsonDataContext(_tempDirectory);
        var supplierRepo = new SupplierRepository(context);
        var productRepo = new ProductRepository(context);
        var movementRepo = new StockMovementRepository(context);

        var pOld = new Product("Café Antiguo", "OLD-01", "Alimentos", null, 5.00m, 8.00m, 5);
        var pRecent = new Product("Café Reciente", "REC-01", "Alimentos", null, 4.00m, 7.00m, 5);
        var pNeverMoved = new Product("Té Nuevo", "NEV-01", "Bebidas", null, 3.00m, 5.00m, 5);
        productRepo.Add(pOld);
        productRepo.Add(pRecent);
        productRepo.Add(pNeverMoved);

        var ticketsJson = $$"""
        [
          { "Id": 1, "Fecha": "{{referenceDate.AddDays(-45):O}}", "ProductoId": {{pOld.Id}}, "Cantidad": 10, "Tipo": 0, "Observaciones": "Compra antigua" },
          { "Id": 2, "Fecha": "{{referenceDate.AddDays(-5):O}}", "ProductoId": {{pRecent.Id}}, "Cantidad": 20, "Tipo": 0, "Observaciones": "Compra reciente" }
        ]
        """;
        File.WriteAllText(Path.Combine(_tempDirectory, "tickets.json"), ticketsJson);

        var reloadedContext = new JsonDataContext(_tempDirectory);
        reloadedContext.LoadJson();
        var reloadedProductRepo = new ProductRepository(reloadedContext);
        var reloadedMovementRepo = new StockMovementRepository(reloadedContext);
        var reloadedSupplierRepo = new SupplierRepository(reloadedContext);
        var inventoryManager = new InventoryManager(reloadedMovementRepo, reloadedProductRepo, reloadedSupplierRepo);

        var valuation = inventoryManager.GetCategoryValuationReport().ToList();
        Assert.Equal(2, valuation.Count);

        var foodValuation = valuation.First(v => v.Category == "Alimentos");
        Assert.Equal(2, foodValuation.ProductCount);
        Assert.Equal(30, foodValuation.TotalUnits);
        Assert.Equal(130.00m, foodValuation.TotalPurchaseCost);
        Assert.Equal(220.00m, foodValuation.TotalSaleValue);
        Assert.Equal(90.00m, foodValuation.EstimatedMargin);

        var lowRotation = inventoryManager.GetLowRotationProducts(30, referenceDate).ToList();
        Assert.Equal(2, lowRotation.Count);
        Assert.Equal("NEV-01", lowRotation[0].ProductSku);
        Assert.Null(lowRotation[0].LastMovementDate);
        Assert.Null(lowRotation[0].DaysWithoutMovement);

        Assert.Equal("OLD-01", lowRotation[1].ProductSku);
        Assert.Equal(45, lowRotation[1].DaysWithoutMovement);

        Assert.Throws<ArgumentOutOfRangeException>(() => inventoryManager.GetLowRotationProducts(0, referenceDate));
    }

    /// <summary>
    /// Verifies valuation CSV and low-rotation CSV generation and file export from <see cref="ReportsViewModel"/>.
    /// </summary>
    [Fact]
    public void ReportsViewModel_AndCsvExporter_GenerateValuationAndLowRotationExports()
    {
        using var provider = BuildSeededProvider();
        var inventoryService = provider.GetRequiredService<IInventoryService>();
        var exportService = provider.GetRequiredService<IInventoryExportService>();

        var valuationCsv = exportService.GenerateValuationCsv(new[]
        {
            new CategoryValuationDto("Alimentos", 4, 208, 1200.00m, 1850.50m, 650.50m)
        });
        Assert.Contains("Categoría,Productos,Stock Total,Costo Compra Total,Valor Venta Total,Margen Estimado", valuationCsv);
        Assert.Contains("Alimentos,4,208,1200.00,1850.50,650.50", valuationCsv);
        Assert.Contains("TOTAL,4,208,1200.00,1850.50,650.50", valuationCsv);

        var lowRotCsv = exportService.GenerateLowRotationCsv(new[]
        {
            new LowRotationProductDto(1, "SKU-X", "Producto Sin Mov", "Limpieza", 0, null, null)
        });
        Assert.Contains("SKU-X,Producto Sin Mov,Limpieza,0,Sin movimientos,—", lowRotCsv);

        var valuationPath = Path.Combine(_tempDirectory, "valoracion.csv");
        var auditPath = Path.Combine(_tempDirectory, "rotacion.csv");
        var dialogStub = new StubReportsDialogService();
        var reportsVm = new ReportsViewModel(inventoryService, exportService, dialogStub);

        dialogStub.NextSavePath = valuationPath;
        reportsVm.ExportValuationCsvCommand.Execute(null);
        Assert.False(reportsVm.IsErrorMessage);
        Assert.True(File.Exists(valuationPath));
        Assert.Contains("TOTAL", File.ReadAllText(valuationPath));

        reportsVm.GenerateLowRotationReportCommand.Execute(null);
        Assert.True(reportsVm.HasGeneratedAudit);
        Assert.Equal(2, reportsVm.LowRotationItems.Count);
        Assert.False(reportsVm.HasNoLowRotationItems);

        dialogStub.NextSavePath = auditPath;
        reportsVm.ExportLowRotationCsvCommand.Execute(null);
        Assert.False(reportsVm.IsErrorMessage);
        Assert.True(File.Exists(auditPath));
        Assert.Contains("Sin movimientos", File.ReadAllText(auditPath));
    }

    /// <summary>
    /// Verifies <see cref="SettingsViewModel"/> validation, JSON persistence across reloads, and live currency/threshold propagation.
    /// </summary>
    [Fact]
    public void SettingsViewModel_ValidatesPersistsAndPropagatesCurrencyAndThreshold()
    {
        using (var provider = BuildSeededProvider())
        {
            var settingsVm = provider.GetRequiredService<SettingsViewModel>();
            var inventoryVm = provider.GetRequiredService<InventoryViewModel>();
            var catalog = provider.GetRequiredService<ICatalogService>();
            var inventory = provider.GetRequiredService<IInventoryService>();

            settingsVm.BusinessName = "";
            settingsVm.LowStockThresholdText = "-2";
            settingsVm.SaveSettingsCommand.Execute(null);

            Assert.True(settingsVm.HasErrors);
            Assert.NotEmpty(settingsVm.GetErrors(nameof(SettingsViewModel.BusinessName)).Cast<string>());
            Assert.NotEmpty(settingsVm.GetErrors(nameof(SettingsViewModel.LowStockThresholdText)).Cast<string>());

            settingsVm.BusinessName = "Bodega Central Norte";
            settingsVm.SelectedCurrencyCode = "EUR";
            settingsVm.LowStockThresholdText = "15";
            settingsVm.SaveSettingsCommand.Execute(null);

            Assert.False(settingsVm.HasErrors);
            Assert.False(settingsVm.IsErrorMessage);
            Assert.Equal("EUR", inventoryVm.CurrencyCode);
            Assert.Equal("€", inventoryVm.CurrencySymbol);
            Assert.Equal("€2,605.10", inventoryVm.FormattedEstimatedValue);

            var productForm = new ProductFormViewModel(catalog, inventory, defaultMinStock: int.Parse(settingsVm.LowStockThresholdText));
            Assert.Equal("15", productForm.MinStockText);

            var settingsService = provider.GetRequiredService<ISettingsService>();
            Assert.Throws<ArgumentException>(() => settingsService.UpdateSettings("Negocio", "GBP", 10));
        }

        using (var reloadedProvider = BuildServiceProviderOnly())
        {
            DependencyInjection.InitializeDatabase(reloadedProvider);
            var reloadedSettingsVm = reloadedProvider.GetRequiredService<SettingsViewModel>();
            var reloadedInventoryVm = reloadedProvider.GetRequiredService<InventoryViewModel>();

            Assert.Equal("Bodega Central Norte", reloadedSettingsVm.BusinessName);
            Assert.Equal("EUR", reloadedSettingsVm.SelectedCurrencyCode);
            Assert.Equal("15", reloadedSettingsVm.LowStockThresholdText);
            Assert.Equal("EUR", reloadedInventoryVm.CurrencyCode);
            Assert.Equal("€2,605.10", reloadedInventoryVm.FormattedEstimatedValue);
        }
    }

    private ServiceProvider BuildSeededProvider()
    {
        var provider = BuildServiceProviderOnly();
        DependencyInjection.InitializeDatabase(provider);
        return provider;
    }

    private ServiceProvider BuildServiceProviderOnly()
    {
        var services = new ServiceCollection();
        services.AddTiendaBodegasServices(_tempDirectory);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Cleans up the temporary directory after each test.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    private sealed class StubReportsDialogService : IDialogService
    {
        public string? NextSavePath { get; set; }

        public bool ShowProductForm(ProductFormViewModel viewModel) => false;
        public bool ShowSupplierForm(SupplierFormViewModel viewModel) => false;
        public bool ShowMovementForm(MovementFormViewModel viewModel) => false;
        public bool Confirm(string message, string title) => false;
        public string? ShowSaveFileDialog(string defaultFileName, string filter) => NextSavePath;
    }
}

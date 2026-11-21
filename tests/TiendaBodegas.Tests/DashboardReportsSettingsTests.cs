using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Application.Services;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Infrastructure.Persistence.Json;
using TiendaBodegas.Infrastructure.Persistence.Repositories;
using TiendaBodegas.Infrastructure.Services;
using TiendaBodegas.UI;
using TiendaBodegas.UI.Converters;
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
    /// Verifies <see cref="SettingsViewModel"/> validation, daily exchange-rate conversion on currency change,
    /// JSON persistence across reloads, and live propagation to inventory and movements.
    /// </summary>
    [Fact]
    public async Task SettingsViewModel_ValidatesPersistsAndConvertsPricesOnCurrencyChange()
    {
        var stubRates = new StubExchangeRateService(new Dictionary<(string From, string To), decimal>
        {
            [("USD", "COP")] = 4000m,
            [("COP", "EUR")] = 0.00025m
        });

        using (var provider = BuildSeededProvider(stubRates))
        {
            var settingsVm = provider.GetRequiredService<SettingsViewModel>();
            var inventoryVm = provider.GetRequiredService<InventoryViewModel>();
            var movementsVm = provider.GetRequiredService<MovementsViewModel>();
            var catalog = provider.GetRequiredService<ICatalogService>();
            var inventory = provider.GetRequiredService<IInventoryService>();

            settingsVm.BusinessName = "";
            settingsVm.LowStockThresholdText = "-2";
            await settingsVm.SaveSettingsCommand.ExecuteAsync(null);

            Assert.True(settingsVm.HasErrors);
            Assert.NotEmpty(settingsVm.GetErrors(nameof(SettingsViewModel.BusinessName)).Cast<string>());
            Assert.NotEmpty(settingsVm.GetErrors(nameof(SettingsViewModel.LowStockThresholdText)).Cast<string>());

            // Switch USD -> COP at 1 USD = 4,000 COP
            settingsVm.BusinessName = "Bodega Central Norte";
            settingsVm.SelectedCurrencyCode = "COP";
            settingsVm.LowStockThresholdText = "12";
            await settingsVm.SaveSettingsCommand.ExecuteAsync(null);

            Assert.False(settingsVm.HasErrors);
            Assert.False(settingsVm.IsErrorMessage);
            Assert.Contains("4,000.00 COP", settingsVm.StatusMessage);
            Assert.Equal("COP", inventoryVm.CurrencyCode);
            Assert.Equal("$", inventoryVm.CurrencySymbol);
            Assert.Equal("PRECIO COMPRA (COP)", inventoryVm.PurchasePriceHeader);
            Assert.Equal("PRECIO VENTA (COP)", inventoryVm.SalePriceHeader);
            Assert.Equal("$10,420,400", inventoryVm.FormattedEstimatedValue);

            var coffeeInCop = catalog.GetAllProducts().First(p => p.SKU == "SKU-10482");
            Assert.Equal(34000m, coffeeInCop.PurchasePrice);
            Assert.Equal(48000m, coffeeInCop.SalePrice);

            var coffeeExitInCop = movementsVm.Movements.First(m => m.ProductSku == "SKU-10482" && m.Quantity == 5);
            Assert.Equal(240000m, coffeeExitInCop.TotalAmount);
            Assert.Equal("COP", movementsVm.CurrencyCode);
            Assert.Equal("$", movementsVm.CurrencySymbol);

            // Colombian thousands-separator parsing in ProductFormViewModel while in COP
            var copProductForm = new ProductFormViewModel(
                catalog,
                inventory,
                defaultMinStock: 10,
                currencySymbol: "$",
                currencyCode: "COP");
            Assert.Equal("PRECIO COMPRA (COP $)", copProductForm.PurchasePriceLabel);
            Assert.Equal("PRECIO VENTA (COP $)", copProductForm.SalePriceLabel);
            copProductForm.Sku = "SKU-COP-1";
            copProductForm.Name = "Café Premium";
            copProductForm.Category = "Bebidas";
            copProductForm.PurchasePriceText = "18.000";
            copProductForm.SalePriceText = "24,500";
            copProductForm.StockText = "0";
            copProductForm.SaveCommand.Execute(null);
            Assert.False(copProductForm.HasErrors);
            var savedCopProduct = catalog.GetAllProducts().First(p => p.SKU == "SKU-COP-1");
            Assert.Equal(18000m, savedCopProduct.PurchasePrice);
            Assert.Equal(24500m, savedCopProduct.SalePrice);
            catalog.DeleteProduct(savedCopProduct.Id);

            // Switch COP -> EUR at 1 COP = 0.00025 EUR (back to 1:1 numeric scale for deterministic assertion)
            settingsVm.SelectedCurrencyCode = "EUR";
            settingsVm.LowStockThresholdText = "15";
            await settingsVm.SaveSettingsCommand.ExecuteAsync(null);

            Assert.False(settingsVm.HasErrors);
            Assert.False(settingsVm.IsErrorMessage);
            Assert.Equal("EUR", inventoryVm.CurrencyCode);
            Assert.Equal("€", inventoryVm.CurrencySymbol);
            Assert.Equal("PRECIO COMPRA (EUR)", inventoryVm.PurchasePriceHeader);
            Assert.Equal("PRECIO VENTA (EUR)", inventoryVm.SalePriceHeader);
            Assert.Equal("€2,605.10", inventoryVm.FormattedEstimatedValue);
            Assert.Equal("EUR", movementsVm.CurrencyCode);
            Assert.Equal("€", movementsVm.CurrencySymbol);

            var productForm = new ProductFormViewModel(
                catalog,
                inventory,
                defaultMinStock: int.Parse(settingsVm.LowStockThresholdText),
                currencySymbol: inventoryVm.CurrencySymbol,
                currencyCode: inventoryVm.CurrencyCode);
            Assert.Equal("15", productForm.MinStockText);
            Assert.Equal("PRECIO COMPRA (EUR €)", productForm.PurchasePriceLabel);
            Assert.Equal("PRECIO VENTA (EUR €)", productForm.SalePriceLabel);

            var settingsService = provider.GetRequiredService<ISettingsService>();
            Assert.Throws<ArgumentException>(() => settingsService.UpdateSettings("Negocio", "MXN", 10));
            Assert.Throws<ArgumentException>(() => settingsService.UpdateSettings("Negocio", "GBP", 10));
        }

        using (var reloadedProvider = BuildServiceProviderOnly(stubRates))
        {
            DependencyInjection.InitializeDatabase(reloadedProvider);
            var reloadedSettingsVm = reloadedProvider.GetRequiredService<SettingsViewModel>();
            var reloadedInventoryVm = reloadedProvider.GetRequiredService<InventoryViewModel>();
            var reloadedCatalog = reloadedProvider.GetRequiredService<ICatalogService>();

            Assert.Equal("Bodega Central Norte", reloadedSettingsVm.BusinessName);
            Assert.Equal("EUR", reloadedSettingsVm.SelectedCurrencyCode);
            Assert.Equal("15", reloadedSettingsVm.LowStockThresholdText);
            Assert.Equal("EUR", reloadedInventoryVm.CurrencyCode);
            Assert.Equal("€2,605.10", reloadedInventoryVm.FormattedEstimatedValue);

            var reloadedCoffee = reloadedCatalog.GetAllProducts().First(p => p.SKU == "SKU-10482");
            Assert.Equal(8.50m, reloadedCoffee.PurchasePrice);
            Assert.Equal(12.00m, reloadedCoffee.SalePrice);
        }
    }

    /// <summary>
    /// Verifies <see cref="DailyExchangeRateService"/> primary parsing, daily caching, secondary failover,
    /// offline reference fallback, and same-currency shortcut.
    /// </summary>
    [Fact]
    public async Task DailyExchangeRateService_HandlesPrimaryCacheSecondaryAndOfflineFallback()
    {
        var today = new DateOnly(2026, 11, 21);

        // 1. Primary API succeeds and caches for the same day
        var primaryCalls = 0;
        var primaryHandler = new StubHttpMessageHandler(request =>
        {
            primaryCalls++;
            var json = """
            {
              "result": "success",
              "time_last_update_utc": "Sat, 21 Nov 2026 00:02:31 +0000",
              "rates": { "USD": 1, "COP": 4125.50, "EUR": 0.93 }
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        using var primaryClient = new HttpClient(primaryHandler);
        var primaryService = new DailyExchangeRateService(primaryClient, () => today);

        var sameCurrency = await primaryService.GetExchangeRateAsync("USD", "USD");
        Assert.Equal(1m, sameCurrency.Rate);
        Assert.Equal(0, primaryCalls);

        var firstQuote = await primaryService.GetExchangeRateAsync("USD", "COP");
        var cachedQuote = await primaryService.GetExchangeRateAsync("USD", "COP");
        Assert.Equal(4125.50m, firstQuote.Rate);
        Assert.False(firstQuote.IsFallback);
        Assert.Equal("open.er-api.com", firstQuote.Source);
        Assert.Equal(firstQuote, cachedQuote);
        Assert.Equal(1, primaryCalls);

        // 2. Primary fails -> secondary jsDelivr currency-api succeeds
        var failoverHandler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.Host.Contains("open.er-api.com", StringComparison.OrdinalIgnoreCase))
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

            var json = """
            {
              "date": "2026-11-21",
              "usd": { "cop": 4180.75, "eur": 0.94 }
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        using var failoverClient = new HttpClient(failoverHandler);
        var failoverService = new DailyExchangeRateService(failoverClient, () => today);
        var secondaryQuote = await failoverService.GetExchangeRateAsync("USD", "COP");
        Assert.Equal(4180.75m, secondaryQuote.Rate);
        Assert.False(secondaryQuote.IsFallback);
        Assert.Equal("currency-api (jsDelivr)", secondaryQuote.Source);

        // 3. Both endpoints offline -> local reference fallback
        var offlineHandler = new StubHttpMessageHandler(_ => throw new HttpRequestException("Offline"));
        using var offlineClient = new HttpClient(offlineHandler);
        var offlineService = new DailyExchangeRateService(offlineClient, () => today);
        var fallbackQuote = await offlineService.GetExchangeRateAsync("USD", "COP");
        Assert.True(fallbackQuote.IsFallback);
        Assert.Equal(4200m, fallbackQuote.Rate);
    }

    /// <summary>
    /// Verifies <see cref="Product.ConvertPrices"/> rounding/guards and <see cref="CurrencyAmountConverter"/> formatting.
    /// </summary>
    [Fact]
    public void ProductConvertPrices_AndCurrencyAmountConverter_BehaveAsExpected()
    {
        var product = new Product("Café", "SKU-1", "Alimentos", null, 8.50m, 12.00m, 5);
        product.ConvertPrices(4150.25m, decimalPlaces: 0);
        Assert.Equal(35277m, product.PurchasePrice);
        Assert.Equal(49803m, product.SalePrice);

        var tinyProduct = new Product("Bolsa", "SKU-2", "Hogar", null, 0m, 1.00m, 5);
        tinyProduct.ConvertPrices(0.0002m);
        Assert.Equal(0.01m, tinyProduct.SalePrice);

        Assert.Throws<ArgumentOutOfRangeException>(() => product.ConvertPrices(0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => product.ConvertPrices(-1m));

        var converter = new CurrencyAmountConverter();
        Assert.Equal("€2,605.10", converter.Convert(new object[] { 2605.10m, "EUR" }, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("COP $49,803", converter.Convert(new object[] { 49803m, "COP" }, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("$12.00", converter.Convert(new object[] { 12.00m, "USD" }, typeof(string), null, CultureInfo.InvariantCulture));
    }

    private ServiceProvider BuildSeededProvider(IExchangeRateService? exchangeRateService = null)
    {
        var provider = BuildServiceProviderOnly(exchangeRateService);
        DependencyInjection.InitializeDatabase(provider);
        return provider;
    }

    private ServiceProvider BuildServiceProviderOnly(IExchangeRateService? exchangeRateService = null)
    {
        var services = new ServiceCollection();
        services.AddTiendaBodegasServices(_tempDirectory);
        if (exchangeRateService != null)
            services.AddSingleton(exchangeRateService);
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

    private sealed class StubExchangeRateService : IExchangeRateService
    {
        private readonly IReadOnlyDictionary<(string From, string To), decimal> _rates;

        public StubExchangeRateService(IReadOnlyDictionary<(string From, string To), decimal> rates)
        {
            _rates = rates;
        }

        public Task<ExchangeRateQuote> GetExchangeRateAsync(
            string fromCurrency,
            string toCurrency,
            CancellationToken cancellationToken = default)
        {
            var from = fromCurrency.ToUpperInvariant();
            var to = toCurrency.ToUpperInvariant();
            var rate = from == to ? 1m : _rates[(from, to)];
            return Task.FromResult(new ExchangeRateQuote(
                from,
                to,
                rate,
                new DateOnly(2026, 11, 21),
                "Stub",
                IsFallback: false));
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }
}

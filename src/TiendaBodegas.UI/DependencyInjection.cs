using Microsoft.Extensions.DependencyInjection;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Application.Services;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Enums;
using TiendaBodegas.Domain.Interfaces;
using TiendaBodegas.Infrastructure.Persistence.Json;
using TiendaBodegas.Infrastructure.Persistence.Repositories;
using TiendaBodegas.Infrastructure.Services;
using TiendaBodegas.UI.Services;
using TiendaBodegas.UI.ViewModels;

namespace TiendaBodegas.UI;

/// <summary>
/// Configures dependency injection and initial database seeding for the desktop application.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence, domain repositories, application services, ViewModels, and the main window.
    /// </summary>
    public static IServiceCollection AddTiendaBodegasServices(this IServiceCollection services, string? dataDirectory = null)
    {
        QuestPdfExportService.ConfigureLicenseOnce();

        services.AddSingleton(_ => new JsonDataContext(dataDirectory));

        services.AddSingleton<ISupplierRepository, SupplierRepository>();
        services.AddSingleton<IProductRepository, ProductRepository>();
        services.AddSingleton<IStockMovementRepository, StockMovementRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<IExchangeRateService, DailyExchangeRateService>();
        services.AddSingleton<IInventoryService, InventoryManager>();
        services.AddSingleton<ICatalogService, CatalogManager>();
        services.AddSingleton<ISettingsService, SettingsManager>();
        services.AddSingleton<IInventoryExportService, InventoryCsvExporter>();
        services.AddSingleton<IPdfExportService, QuestPdfExportService>();
        services.AddSingleton<IPrinterService, WpfPrinterService>();
        services.AddSingleton<IDialogService, DialogService>();

        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<InventoryViewModel>();
        services.AddSingleton<SuppliersViewModel>();
        services.AddSingleton<MovementsViewModel>();
        services.AddSingleton<ReportsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }

    /// <summary>
    /// Loads persisted JSON data and populates sample seed records when the database is empty.
    /// </summary>
    public static void InitializeDatabase(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<JsonDataContext>();
        context.LoadJson();

        if (context.Suppliers.Any() || context.Products.Any())
            return;

        var supplierRepository = serviceProvider.GetRequiredService<ISupplierRepository>();
        var productRepository = serviceProvider.GetRequiredService<IProductRepository>();
        var inventoryService = serviceProvider.GetRequiredService<IInventoryService>();

        var north = new Supplier("Distribuidora del Norte S.A.", "contacto@distnorte.com", "Alimentos", SupplierStatus.Active);
        var chemicals = new Supplier("Químicos y Limpieza Global", "ventas@qlglobal.com", "Limpieza", SupplierStatus.Active);
        var home = new Supplier("Insumos Hogar y Papel", "pedidos@insumoshogar.com", "Hogar", SupplierStatus.UnderReview);
        var beverages = new Supplier("Bebidas Andinas S.A.", "comercial@bebidasandinas.com", "Bebidas", SupplierStatus.Active);

        supplierRepository.Add(north);
        supplierRepository.Add(chemicals);
        supplierRepository.Add(home);
        supplierRepository.Add(beverages);

        var coffee = new Product("Café Espresso Molido 1kg", "SKU-10482", "Alimentos", north.Id, 8.50m, 12.00m, minStock: 10);
        var oliveOil = new Product("Aceite de Oliva Extra Virgen", "SKU-20948", "Alimentos", north.Id, 12.00m, 18.50m, minStock: 10);
        var detergent = new Product("Detergente Líquido 3L", "SKU-50291", "Limpieza", chemicals.Id, 5.20m, 8.90m, minStock: 10);
        var napkins = new Product("Paquete de Servilletas 400", "SKU-40281", "Hogar", home.Id, 1.50m, 2.80m, minStock: 10);
        var oatmeal = new Product("Cereal de Avena Orgánica", "SKU-30912", "Alimentos", north.Id, 3.10m, 5.00m, minStock: 10);
        var disinfectant = new Product("Desinfectante Multiusos 2L", "SKU-61820", "Limpieza", chemicals.Id, 2.40m, 4.50m, minStock: 10);
        var water = new Product("Agua Mineral con Gas (Pack 12)", "SKU-77412", "Bebidas", beverages.Id, 6.00m, 9.50m, minStock: 10);
        var toiletPaper = new Product("Papel Higiénico Doble Hoja 12u", "SKU-88201", "Hogar", home.Id, 4.80m, 7.20m, minStock: 10);
        var greenTea = new Product("Té Verde en Hebras 250g", "SKU-90311", "Bebidas", beverages.Id, 4.10m, 6.90m, minStock: 10);
        var rice = new Product("Arroz Integral Grano Largo 1kg", "SKU-11294", "Alimentos", north.Id, 1.80m, 3.10m, minStock: 10);
        var sponges = new Product("Esponjas Doble Fibra (Pack 6)", "SKU-55019", "Limpieza", chemicals.Id, 1.20m, 2.50m, minStock: 10);
        var bioBags = new Product("Bolsas Biodegradables 50L", "SKU-44102", "Hogar", home.Id, 2.90m, 4.90m, minStock: 10);

        productRepository.Add(coffee);
        productRepository.Add(oliveOil);
        productRepository.Add(detergent);
        productRepository.Add(napkins);
        productRepository.Add(oatmeal);
        productRepository.Add(disinfectant);
        productRepository.Add(water);
        productRepository.Add(toiletPaper);
        productRepository.Add(greenTea);
        productRepository.Add(rice);
        productRepository.Add(sponges);
        productRepository.Add(bioBags);

        inventoryService.RegisterEntry(coffee.Id, 50, "Stock inicial");
        inventoryService.RegisterEntry(oliveOil.Id, 11, "Stock inicial");
        inventoryService.RegisterEntry(napkins.Id, 132, "Stock inicial");
        inventoryService.RegisterEntry(oatmeal.Id, 67, "Stock inicial");
        inventoryService.RegisterEntry(disinfectant.Id, 9, "Stock inicial");
        inventoryService.RegisterEntry(water.Id, 34, "Stock inicial");
        inventoryService.RegisterEntry(toiletPaper.Id, 52, "Stock inicial");
        inventoryService.RegisterEntry(greenTea.Id, 5, "Stock inicial");
        inventoryService.RegisterEntry(rice.Id, 88, "Stock inicial");
        inventoryService.RegisterEntry(bioBags.Id, 41, "Stock inicial");

        inventoryService.RegisterExit(oliveOil.Id, 3, "Orden #V-2039 — Salida de Stock");
        inventoryService.RegisterExit(napkins.Id, 12, "Orden #V-2040 — Salida de Stock");
        inventoryService.RegisterExit(coffee.Id, 5, "Orden #V-2041 — Salida de Stock");
    }
}

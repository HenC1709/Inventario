using Microsoft.Extensions.DependencyInjection;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Application.Services;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Interfaces;
using TiendaBodegas.Infrastructure.Persistence.Json;
using TiendaBodegas.Infrastructure.Persistence.Repositories;
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
        services.AddSingleton(_ => new JsonDataContext(dataDirectory));

        services.AddSingleton<IWarehouseRepository, WarehouseRepository>();
        services.AddSingleton<IProductRepository, ProductRepository>();
        services.AddSingleton<IStockMovementRepository, StockMovementRepository>();
        services.AddSingleton<IInventoryService, InventoryManager>();

        services.AddSingleton<WarehousesViewModel>();
        services.AddSingleton<ProductsViewModel>();
        services.AddSingleton<MovementsViewModel>();
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

        if (context.Warehouses.Any() || context.Products.Any())
            return;

        var warehouseRepository = serviceProvider.GetRequiredService<IWarehouseRepository>();
        var productRepository = serviceProvider.GetRequiredService<IProductRepository>();
        var inventoryService = serviceProvider.GetRequiredService<IInventoryService>();

        var central = new Warehouse("Bodega Central", "Av. Principal 120");
        var north = new Warehouse("Bodega Norte", "Calle Los Pinos 450");
        var south = new Warehouse("Bodega Sur", "Av. Industrial 890");
        warehouseRepository.Add(central);
        warehouseRepository.Add(north);
        warehouseRepository.Add(south);

        var rice = new Product("Arroz Extra 1kg", "ARR-001", 1.85m);
        var oil = new Product("Aceite Vegetal 1L", "ACE-001", 3.40m);
        var sugar = new Product("Azúcar Rubia 1kg", "AZU-001", 1.50m);
        var milk = new Product("Leche Evaporada 400g", "LEC-001", 1.25m);
        productRepository.Add(rice);
        productRepository.Add(oil);
        productRepository.Add(sugar);
        productRepository.Add(milk);

        inventoryService.RegisterEntry(rice.Id, central.Id, 100, "Stock inicial");
        inventoryService.RegisterEntry(oil.Id, central.Id, 60, "Stock inicial");
        inventoryService.RegisterEntry(sugar.Id, north.Id, 80, "Stock inicial");
        inventoryService.RegisterEntry(milk.Id, south.Id, 120, "Stock inicial");
        inventoryService.TransferStock(rice.Id, central.Id, north.Id, 25, "Distribución inicial");
        inventoryService.TransferStock(oil.Id, central.Id, south.Id, 15, "Distribución inicial");
    }
}

using System.IO;
using Microsoft.Extensions.DependencyInjection;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Interfaces;
using TiendaBodegas.UI;
using TiendaBodegas.UI.ViewModels;
using Xunit;

namespace TiendaBodegas.Tests;

/// <summary>
/// Unit and integration tests covering dependency injection, initial prototype seeding, and 6-section shell navigation.
/// </summary>
public class Mvp1ViewModelsAndDiTests : IDisposable
{
    private readonly string _tempDirectory;

    /// <summary>
    /// Initializes an isolated temporary data directory for each test run.
    /// </summary>
    public Mvp1ViewModelsAndDiTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "TiendaBodegasMvp1Tests_" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Verifies that empty storage receives the prototype seed data on startup, computes expected KPI totals, and avoids duplicating on reload.
    /// </summary>
    [Fact]
    public void InitializeDatabase_WhenEmpty_SeedsDataAndPersistsAcrossReloads()
    {
        using (var firstProvider = BuildServiceProvider())
        {
            DependencyInjection.InitializeDatabase(firstProvider);

            Assert.True(File.Exists(Path.Combine(_tempDirectory, "tiendabodegas.db")));

            Assert.Equal(4, firstProvider.GetRequiredService<ISupplierRepository>().GetAll().Count());
            Assert.Equal(12, firstProvider.GetRequiredService<IProductRepository>().GetAll().Count());
            Assert.Equal(13, firstProvider.GetRequiredService<IStockMovementRepository>().GetAll().Count());

            var inventoryService = firstProvider.GetRequiredService<IInventoryService>();
            var summary = inventoryService.GetProductStockSummary().ToList();
            Assert.Equal(12, summary.Count);
            Assert.Equal(5, summary.Count(s => s.IsLowStock));
            Assert.Equal(4, summary.Select(s => s.Category).Distinct().Count());
            Assert.Equal(2605.10m, summary.Sum(s => s.SalePrice * s.TotalStock));
        }

        using (var secondProvider = BuildServiceProvider())
        {
            DependencyInjection.InitializeDatabase(secondProvider);

            Assert.Equal(4, secondProvider.GetRequiredService<ISupplierRepository>().GetAll().Count());
            Assert.Equal(12, secondProvider.GetRequiredService<IProductRepository>().GetAll().Count());
            Assert.Equal(13, secondProvider.GetRequiredService<IStockMovementRepository>().GetAll().Count());
        }
    }

    /// <summary>
    /// Verifies that <see cref="MainViewModel"/> navigates across all 6 sidebar sections, tracks active state, and updates the inventory badge.
    /// </summary>
    [Fact]
    public void MainViewModel_NavigationCommands_SwitchCurrentViewModelAndSyncBadge()
    {
        using var provider = BuildServiceProvider();
        DependencyInjection.InitializeDatabase(provider);

        var mainViewModel = provider.GetRequiredService<MainViewModel>();

        Assert.IsType<InventoryViewModel>(mainViewModel.CurrentViewModel);
        Assert.Equal(NavigationSection.Inventory, mainViewModel.ActiveSection);
        Assert.True(mainViewModel.IsInventoryActive);
        Assert.Equal("Inventario", mainViewModel.CurrentSectionTitle);
        Assert.Equal(12, mainViewModel.InventoryCount);

        mainViewModel.ShowDashboardCommand.Execute(null);
        Assert.IsType<DashboardViewModel>(mainViewModel.CurrentViewModel);
        Assert.True(mainViewModel.IsDashboardActive);
        Assert.False(mainViewModel.IsInventoryActive);
        Assert.Equal("Dashboard", mainViewModel.CurrentSectionTitle);

        mainViewModel.ShowSuppliersCommand.Execute(null);
        var suppliersVm = Assert.IsType<SuppliersViewModel>(mainViewModel.CurrentViewModel);
        Assert.True(mainViewModel.IsSuppliersActive);
        Assert.Equal("Proveedores", mainViewModel.CurrentSectionTitle);
        Assert.Equal(4, suppliersVm.Suppliers.Count);

        mainViewModel.ShowSalesCommand.Execute(null);
        var movementsVm = Assert.IsType<MovementsViewModel>(mainViewModel.CurrentViewModel);
        Assert.True(mainViewModel.IsSalesActive);
        Assert.Equal("Ventas", mainViewModel.CurrentSectionTitle);
        Assert.Equal(13, movementsVm.Movements.Count);

        mainViewModel.ShowReportsCommand.Execute(null);
        Assert.IsType<ReportsViewModel>(mainViewModel.CurrentViewModel);
        Assert.True(mainViewModel.IsReportsActive);
        Assert.Equal("Reportes", mainViewModel.CurrentSectionTitle);

        mainViewModel.ShowSettingsCommand.Execute(null);
        Assert.IsType<SettingsViewModel>(mainViewModel.CurrentViewModel);
        Assert.True(mainViewModel.IsSettingsActive);
        Assert.Equal("Ajustes", mainViewModel.CurrentSectionTitle);

        var catalogService = provider.GetRequiredService<ICatalogService>();
        catalogService.CreateProduct("Producto Extra", "EXT-001", "Alimentos", 1, 2.00m, 4.00m, 5);

        mainViewModel.ShowInventoryCommand.Execute(null);
        var inventoryVm = Assert.IsType<InventoryViewModel>(mainViewModel.CurrentViewModel);
        Assert.True(mainViewModel.IsInventoryActive);
        Assert.Equal(13, inventoryVm.TotalProductsCount);
        Assert.Equal(13, mainViewModel.InventoryCount);
    }

    /// <summary>
    /// Builds an isolated DI container configured against the temporary test folder.
    /// </summary>
    private ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddTiendaBodegasServices(_tempDirectory);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Cleans up the temporary test directory after execution.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }
}

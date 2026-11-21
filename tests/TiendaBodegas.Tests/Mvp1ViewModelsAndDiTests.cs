using System.IO;
using Microsoft.Extensions.DependencyInjection;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Infrastructure.Persistence.Json;
using TiendaBodegas.UI;
using TiendaBodegas.UI.ViewModels;
using Xunit;

namespace TiendaBodegas.Tests;

/// <summary>
/// Unit and integration tests covering dependency injection, initial prototype seeding, and navigation ViewModels.
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

            var firstContext = firstProvider.GetRequiredService<JsonDataContext>();
            Assert.Equal(4, firstContext.Suppliers.Count);
            Assert.Equal(12, firstContext.Products.Count);
            Assert.Equal(13, firstContext.StockMovements.Count);

            var inventoryService = firstProvider.GetRequiredService<IInventoryService>();
            var summary = inventoryService.GetProductStockSummary().ToList();
            Assert.Equal(12, summary.Count);
            Assert.Equal(5, summary.Count(s => s.IsLowStock));
            Assert.Equal(4, summary.Select(s => s.Category).Distinct().Count());
            Assert.Equal(2605.10m, summary.Sum(s => s.SalePrice * s.TotalStock));

            firstContext.SaveJson();
        }

        using (var secondProvider = BuildServiceProvider())
        {
            DependencyInjection.InitializeDatabase(secondProvider);

            var secondContext = secondProvider.GetRequiredService<JsonDataContext>();
            Assert.Equal(4, secondContext.Suppliers.Count);
            Assert.Equal(12, secondContext.Products.Count);
            Assert.Equal(13, secondContext.StockMovements.Count);
        }
    }

    /// <summary>
    /// Verifies that <see cref="MainViewModel"/> commands switch active ViewModels, update titles, and populate collections.
    /// </summary>
    [Fact]
    public void MainViewModel_NavigationCommands_SwitchCurrentViewModelAndLoadData()
    {
        using var provider = BuildServiceProvider();
        DependencyInjection.InitializeDatabase(provider);

        var mainViewModel = provider.GetRequiredService<MainViewModel>();

        Assert.IsType<SuppliersViewModel>(mainViewModel.CurrentViewModel);
        Assert.Equal("Proveedores", mainViewModel.CurrentSectionTitle);

        mainViewModel.ShowProductsCommand.Execute(null);
        var productsVm = Assert.IsType<ProductsViewModel>(mainViewModel.CurrentViewModel);
        Assert.Equal("Productos", mainViewModel.CurrentSectionTitle);
        Assert.Equal(12, productsVm.Products.Count);

        mainViewModel.ShowStockCommand.Execute(null);
        var stockVm = Assert.IsType<StockViewModel>(mainViewModel.CurrentViewModel);
        Assert.Equal("Stock", mainViewModel.CurrentSectionTitle);
        Assert.Equal(12, stockVm.ProductTotals.Count);
        Assert.Equal(5, stockVm.LowTotalProductCount);

        mainViewModel.ShowMovementsCommand.Execute(null);
        var movementsVm = Assert.IsType<MovementsViewModel>(mainViewModel.CurrentViewModel);
        Assert.Equal("Movimientos", mainViewModel.CurrentSectionTitle);
        Assert.Equal(13, movementsVm.Movements.Count);

        mainViewModel.ShowSuppliersCommand.Execute(null);
        var suppliersVm = Assert.IsType<SuppliersViewModel>(mainViewModel.CurrentViewModel);
        Assert.Equal("Proveedores", mainViewModel.CurrentSectionTitle);
        Assert.Equal(4, suppliersVm.Suppliers.Count);
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

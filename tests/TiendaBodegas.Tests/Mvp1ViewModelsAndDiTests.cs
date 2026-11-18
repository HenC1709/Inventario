using System.IO;
using Microsoft.Extensions.DependencyInjection;
using TiendaBodegas.Infrastructure.Persistence.Json;
using TiendaBodegas.UI;
using TiendaBodegas.UI.ViewModels;
using Xunit;

namespace TiendaBodegas.Tests;

/// <summary>
/// Unit and integration tests covering MVP 1 dependency injection, initial seeding, and navigation ViewModels.
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
    /// Verifies that empty storage receives seed data on startup and subsequent startups reload without duplicating records.
    /// </summary>
    [Fact]
    public void InitializeDatabase_WhenEmpty_SeedsDataAndPersistsAcrossReloads()
    {
        using (var firstProvider = BuildServiceProvider())
        {
            DependencyInjection.InitializeDatabase(firstProvider);

            var firstContext = firstProvider.GetRequiredService<JsonDataContext>();
            Assert.Equal(3, firstContext.Warehouses.Count);
            Assert.Equal(4, firstContext.Products.Count);
            Assert.Equal(6, firstContext.StockMovements.Count);

            firstContext.SaveJson();
        }

        using (var secondProvider = BuildServiceProvider())
        {
            DependencyInjection.InitializeDatabase(secondProvider);

            var secondContext = secondProvider.GetRequiredService<JsonDataContext>();
            Assert.Equal(3, secondContext.Warehouses.Count);
            Assert.Equal(4, secondContext.Products.Count);
            Assert.Equal(6, secondContext.StockMovements.Count);
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

        Assert.IsType<WarehousesViewModel>(mainViewModel.CurrentViewModel);
        Assert.Equal("Bodegas", mainViewModel.CurrentSectionTitle);

        mainViewModel.ShowProductsCommand.Execute(null);
        var productsVm = Assert.IsType<ProductsViewModel>(mainViewModel.CurrentViewModel);
        Assert.Equal("Productos", mainViewModel.CurrentSectionTitle);
        Assert.Equal(4, productsVm.Products.Count);

        mainViewModel.ShowMovementsCommand.Execute(null);
        var movementsVm = Assert.IsType<MovementsViewModel>(mainViewModel.CurrentViewModel);
        Assert.Equal("Movimientos", mainViewModel.CurrentSectionTitle);
        Assert.Equal(6, movementsVm.Movements.Count);

        mainViewModel.ShowWarehousesCommand.Execute(null);
        var warehousesVm = Assert.IsType<WarehousesViewModel>(mainViewModel.CurrentViewModel);
        Assert.Equal("Bodegas", mainViewModel.CurrentSectionTitle);
        Assert.Equal(3, warehousesVm.Warehouses.Count);
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

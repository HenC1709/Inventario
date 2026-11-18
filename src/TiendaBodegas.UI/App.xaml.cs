using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TiendaBodegas.Infrastructure.Persistence.Json;

namespace TiendaBodegas.UI;

/// <summary>
/// WPF application entry point responsible for configuring DI, loading persisted JSON, and saving on exit.
/// </summary>
public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    /// <summary>
    /// Initializes the DI container, loads or seeds JSON data, and displays the main window.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddTiendaBodegasServices();
        _serviceProvider = services.BuildServiceProvider();

        DependencyInjection.InitializeDatabase(_serviceProvider);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    /// <summary>
    /// Persists JSON data and disposes the DI container when the application exits.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        if (_serviceProvider != null)
        {
            _serviceProvider.GetRequiredService<JsonDataContext>().SaveJson();
            _serviceProvider.Dispose();
        }

        base.OnExit(e);
    }
}

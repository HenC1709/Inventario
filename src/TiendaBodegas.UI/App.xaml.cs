using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace TiendaBodegas.UI;

/// <summary>
/// WPF application entry point responsible for configuring DI, initializing SQLite persistence, and disposing resources on exit.
/// </summary>
public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    /// <summary>
    /// Initializes the DI container, migrates or seeds SQLite data, and displays the main window.
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
    /// Disposes the DI container and releases SQLite resources when the application exits.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}

using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using TiendaBodegas.Application.Interfaces;

namespace TiendaBodegas.UI;

/// <summary>
/// WPF application entry point responsible for configuring DI, initializing SQLite persistence,
/// capturing unhandled exceptions, and disposing resources on exit.
/// </summary>
public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    /// <summary>
    /// Initializes the DI container, migrates or seeds SQLite data, registers global error handlers, and displays the main window.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var services = new ServiceCollection();
        services.AddTiendaBodegasServices();
        _serviceProvider = services.BuildServiceProvider();

        DependencyInjection.InitializeDatabase(_serviceProvider);
        _serviceProvider.GetService<IAppLogger>()?.Info("StockFlow iniciado correctamente.");

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    /// <summary>
    /// Logs an unhandled exception with technical detail and returns a safe, user-friendly Spanish message
    /// without exposing stack traces or internal file paths.
    /// </summary>
    public static string ProcessUnhandledException(Exception exception, IAppLogger? logger, string context = "UI")
    {
        ArgumentNullException.ThrowIfNull(exception);

        logger?.Error($"Excepción no controlada ({context}).", exception);

        return exception is IOException or UnauthorizedAccessException
            ? "Ocurrió un problema al acceder al almacenamiento local de StockFlow. El detalle técnico se registró en el archivo de diagnóstico local para su revisión."
            : "Ocurrió un problema inesperado en StockFlow. El detalle técnico se registró en el archivo de diagnóstico local para su revisión.";
    }

    /// <summary>
    /// Unsubscribes global exception handlers, logs shutdown, and disposes the DI container when the application exits.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

        if (_serviceProvider != null)
        {
            _serviceProvider.GetService<IAppLogger>()?.Info("Cerrando StockFlow.");
            _serviceProvider.Dispose();
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var logger = _serviceProvider?.GetService<IAppLogger>();
        var userMessage = ProcessUnhandledException(e.Exception, logger, "Dispatcher");

        MessageBox.Show(
            userMessage,
            "StockFlow — Aviso del Sistema",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            var logger = _serviceProvider?.GetService<IAppLogger>();
            ProcessUnhandledException(ex, logger, "AppDomain");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var logger = _serviceProvider?.GetService<IAppLogger>();
        ProcessUnhandledException(e.Exception, logger, "TaskScheduler");
        e.SetObserved();
    }
}

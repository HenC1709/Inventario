using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Enums;
using TiendaBodegas.Infrastructure.Persistence;
using TiendaBodegas.Infrastructure.Services;
using TiendaBodegas.UI;
using TiendaBodegas.UI.Services;
using TiendaBodegas.UI.ViewModels;
using Xunit;

namespace TiendaBodegas.Tests;

/// <summary>
/// Integration tests covering N5 (<c>%LOCALAPPDATA%\StockFlow</c> path resolution and legacy promotion,
/// SQLite backup/restore with validation and pre-restore safety copies, Serilog diagnostics, and unhandled exception formatting).
/// </summary>
public class BackupObservabilityAndAppDataTests : IDisposable
{
    private readonly string _tempDirectory;

    /// <summary>
    /// Initializes an isolated temporary root directory for each test execution.
    /// </summary>
    public BackupObservabilityAndAppDataTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "StockFlowN5Tests_" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Verifies default <c>%LOCALAPPDATA%\StockFlow</c> resolution and non-destructive promotion of legacy files
    /// into an empty user data directory.
    /// </summary>
    [Fact]
    public void AppDataPaths_ResolvesLocalAppDataAndPromotesLegacyFilesWithoutDeletingOriginals()
    {
        var defaultDir = AppDataPaths.GetDefaultDataDirectory();
        Assert.Equal("StockFlow", Path.GetFileName(defaultDir));

        var legacyDir = Path.Combine(_tempDirectory, "legacy-data");
        var targetDir = Path.Combine(_tempDirectory, "user-stockflow");
        Directory.CreateDirectory(legacyDir);

        File.WriteAllText(Path.Combine(legacyDir, "settings.json"), """{"NombreNegocio":"Legado","MonedaPrincipal":"USD","UmbralBajoStock":10}""");
        File.WriteAllBytes(Path.Combine(legacyDir, "tiendabodegas.db"), [1, 2, 3, 4]);

        var promoted = AppDataPaths.PromoteLegacyDataIfTargetEmpty(targetDir, legacyDir);
        Assert.True(promoted);
        Assert.True(File.Exists(Path.Combine(targetDir, "settings.json")));
        Assert.True(File.Exists(Path.Combine(targetDir, "tiendabodegas.db")));
        Assert.True(File.Exists(Path.Combine(legacyDir, "settings.json")));
        Assert.True(File.Exists(Path.Combine(legacyDir, "tiendabodegas.db")));

        var secondAttempt = AppDataPaths.PromoteLegacyDataIfTargetEmpty(targetDir, legacyDir);
        Assert.False(secondAttempt);

        var customResolved = AppDataPaths.ResolveDataDirectory(Path.Combine(_tempDirectory, "custom-isolated"));
        Assert.True(Directory.Exists(customResolved));
    }

    /// <summary>
    /// Verifies SQLite backup creation, invalid/foreign file rejection, automatic pre-restore safety copies,
    /// and live reload of ViewModels through <see cref="IDatabaseBackupService.DatabaseRestored"/>.
    /// </summary>
    [Fact]
    public void SqliteBackupService_CreatesValidBackup_RestoresWithPreRestoreSafetyCopy_AndRejectsInvalidFiles()
    {
        var dataDir = Path.Combine(_tempDirectory, "appdata");
        var backupPath = Path.Combine(_tempDirectory, "exports", "respaldo_valido.db");
        var corruptPath = Path.Combine(_tempDirectory, "corrupto.db");
        var foreignDbPath = Path.Combine(_tempDirectory, "ajeno.db");

        using var provider = BuildSeededProvider(dataDir);
        var backupService = provider.GetRequiredService<IDatabaseBackupService>();
        var catalog = provider.GetRequiredService<ICatalogService>();
        var settingsService = provider.GetRequiredService<ISettingsService>();
        var mainVm = provider.GetRequiredService<MainViewModel>();
        var inventoryVm = provider.GetRequiredService<InventoryViewModel>();
        var settingsVm = provider.GetRequiredService<SettingsViewModel>();

        Assert.Equal(12, mainVm.InventoryCount);
        Assert.Equal("USD", inventoryVm.CurrencyCode);

        Assert.Throws<InvalidOperationException>(() =>
            backupService.CreateBackup(backupService.ActiveDatabasePath));

        backupService.CreateBackup(backupPath);
        Assert.True(File.Exists(backupPath));

        // Reject invalid / foreign candidate files
        File.WriteAllText(corruptPath, "esto no es un archivo sqlite");
        Assert.Throws<InvalidOperationException>(() => backupService.RestoreBackup(corruptPath));
        Assert.Throws<FileNotFoundException>(() => backupService.RestoreBackup(Path.Combine(_tempDirectory, "inexistente.db")));

        using (var foreignConn = new SqliteConnection($"Data Source={foreignDbPath};Pooling=false"))
        {
            foreignConn.Open();
            using var cmd = foreignConn.CreateCommand();
            cmd.CommandText = "CREATE TABLE OtraTabla (Id INTEGER PRIMARY KEY); INSERT INTO OtraTabla VALUES (1);";
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();
        Assert.Throws<InvalidOperationException>(() => backupService.RestoreBackup(foreignDbPath));

        // Mutate live database after backup
        var extraSupplier = catalog.CreateSupplier("Proveedor Temporal", "temp@stockflow.co", "Otros", SupplierStatus.Active);
        catalog.CreateProduct("Producto Temporal", "TMP-999", "Otros", extraSupplier.Id, 10m, 20m, 5);
        settingsService.UpdateSettings("Tienda Mutada", "COP", 20);
        mainVm.ShowInventoryCommand.Execute(null);
        Assert.Equal(13, mainVm.InventoryCount);

        // Restore original backup -> pre-restore safety copy created + ViewModels synchronized
        backupService.RestoreBackup(backupPath);

        var backupsDir = Path.Combine(dataDir, "backups");
        Assert.True(Directory.Exists(backupsDir));
        Assert.NotEmpty(Directory.GetFiles(backupsDir, "pre-restore-*.db"));

        Assert.Equal(12, catalog.GetAllProducts().Count());
        Assert.Equal(4, catalog.GetAllSuppliers().Count());
        Assert.Equal(12, mainVm.InventoryCount);
        Assert.Equal("USD", inventoryVm.CurrencyCode);
        Assert.Equal("USD", settingsVm.SelectedCurrencyCode);
    }

    /// <summary>
    /// Verifies <see cref="SettingsViewModel"/> backup and restore commands including user confirmation cancellation.
    /// </summary>
    [Fact]
    public void SettingsViewModel_BackupAndRestoreCommands_RespectConfirmationAndUpdateStatus()
    {
        var dataDir = Path.Combine(_tempDirectory, "vm-appdata");
        var backupFile = Path.Combine(_tempDirectory, "manual-backup.db");

        using var provider = BuildSeededProvider(dataDir);
        var settingsService = provider.GetRequiredService<ISettingsService>();
        var exchangeService = provider.GetRequiredService<IExchangeRateService>();
        var backupService = provider.GetRequiredService<IDatabaseBackupService>();
        var catalog = provider.GetRequiredService<ICatalogService>();
        var logger = provider.GetRequiredService<IAppLogger>();

        var dialogStub = new StubBackupDialogService { NextSavePath = backupFile };
        var settingsVm = new SettingsViewModel(settingsService, exchangeService, backupService, dialogStub, logger);

        Assert.True(settingsVm.HasBackupService);
        Assert.Equal(dataDir, settingsVm.DataDirectoryPath);

        settingsVm.CreateBackupCommand.Execute(null);
        Assert.False(settingsVm.IsErrorMessage);
        Assert.True(File.Exists(backupFile));
        Assert.Contains("Copia de seguridad creada", settingsVm.StatusMessage);

        catalog.CreateProduct("Extra Backup Test", "BKP-001", "Alimentos", 1, 5m, 10m, 2);
        Assert.Equal(13, catalog.GetAllProducts().Count());

        // Cancelled restore leaves mutated data intact
        dialogStub.NextOpenPath = backupFile;
        dialogStub.ConfirmResult = false;
        settingsVm.RestoreBackupCommand.Execute(null);
        Assert.Equal(13, catalog.GetAllProducts().Count());

        // Confirmed restore restores original 12 products
        dialogStub.ConfirmResult = true;
        settingsVm.RestoreBackupCommand.Execute(null);
        Assert.False(settingsVm.IsErrorMessage);
        Assert.Contains("Copia de seguridad restaurada", settingsVm.StatusMessage);
        Assert.Equal(12, catalog.GetAllProducts().Count());
    }

    /// <summary>
    /// Verifies that <see cref="SerilogAppLogger"/> writes UTF-8 rolling logs, releases file locks on disposal,
    /// and <see cref="App.ProcessUnhandledException"/> returns a safe Spanish message without internal details.
    /// </summary>
    [Fact]
    public void SerilogAppLogger_AndUnhandledExceptionProcessor_WriteUtf8LogsAndReturnSafeUserMessage()
    {
        var logsDir = Path.Combine(_tempDirectory, "logs");
        string safeMessage;

        using (var logger = new SerilogAppLogger(logsDir))
        {
            logger.Info("Inicio de prueba diagnóstica.");
            logger.Warning("Advertencia de prueba.");

            var internalEx = new InvalidOperationException(@"Fallo interno en C:\ruta\privada\secreto.db");
            safeMessage = App.ProcessUnhandledException(internalEx, logger, "Dispatcher");
        }

        Assert.Contains("StockFlow", safeMessage);
        Assert.DoesNotContain(@"C:\ruta\privada", safeMessage);
        Assert.DoesNotContain("InvalidOperationException", safeMessage);

        var logFiles = Directory.GetFiles(logsDir, "stockflow-*.log");
        var logFile = Assert.Single(logFiles);
        var logContent = File.ReadAllText(logFile);
        Assert.Contains("Inicio de prueba diagnóstica.", logContent);
        Assert.Contains("Excepción no controlada (Dispatcher).", logContent);
        Assert.Contains(@"C:\ruta\privada\secreto.db", logContent);
    }

    private static ServiceProvider BuildSeededProvider(string dataDirectory)
    {
        var services = new ServiceCollection();
        services.AddTiendaBodegasServices(dataDirectory);
        var provider = services.BuildServiceProvider();
        DependencyInjection.InitializeDatabase(provider);
        return provider;
    }

    /// <summary>
    /// Cleans up temporary test folders after releasing SQLite pools.
    /// </summary>
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    private sealed class StubBackupDialogService : IDialogService
    {
        public string? NextSavePath { get; set; }
        public string? NextOpenPath { get; set; }
        public bool ConfirmResult { get; set; }

        public bool ShowProductForm(ProductFormViewModel viewModel) => false;
        public bool ShowSupplierForm(SupplierFormViewModel viewModel) => false;
        public bool ShowMovementForm(MovementFormViewModel viewModel) => false;
        public bool Confirm(string message, string title) => ConfirmResult;
        public string? ShowSaveFileDialog(string defaultFileName, string filter) => NextSavePath;
        public string? ShowOpenFileDialog(string filter) => NextOpenPath;
    }
}

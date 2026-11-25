using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Enums;
using TiendaBodegas.Infrastructure.Persistence.Sqlite;
using TiendaBodegas.Infrastructure.Persistence.Sqlite.Repositories;
using TiendaBodegas.UI;
using Xunit;

namespace TiendaBodegas.Tests;

/// <summary>
/// Integration tests covering N4 (SQLite schema migrations, relational repositories, foreign-key integrity,
/// and transactional JSON-to-SQLite migration with balance reconciliation).
/// </summary>
public class SqlitePersistenceAndMigrationTests : IDisposable
{
    private readonly string _tempDirectory;

    /// <summary>
    /// Initializes an isolated temporary directory for each test execution.
    /// </summary>
    public SqlitePersistenceAndMigrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "TiendaBodegasN4Tests_" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Verifies that <see cref="SqliteSchemaMigrator"/> creates <c>tiendabodegas.db</c>, tracks version 1 in
    /// <c>SchemaVersions</c>, and behaves idempotently across repeated invocations.
    /// </summary>
    [Fact]
    public void SchemaMigrator_AppliesVersion1Idempotently()
    {
        using var context = new SqliteDataContext(_tempDirectory);
        context.InitializeSchema();
        context.InitializeSchema();
        SqliteSchemaMigrator.Migrate(context);

        Assert.True(File.Exists(context.DatabasePath));
        var versions = SqliteSchemaMigrator.GetAppliedVersions(context);
        Assert.Equal(new[] { 1 }, versions);
    }

    /// <summary>
    /// Verifies CRUD operations, batch price updates, case-insensitive lookups, exact movement timestamps,
    /// settings upsert, and foreign-key <c>ON DELETE RESTRICT</c> constraints across SQLite repositories.
    /// </summary>
    [Fact]
    public void SqliteRepositories_PerformCrudBatchUpdateAndEnforceForeignKeys()
    {
        using var context = new SqliteDataContext(_tempDirectory);
        var supplierRepo = new SqliteSupplierRepository(context);
        var productRepo = new SqliteProductRepository(context);
        var movementRepo = new SqliteStockMovementRepository(context);
        var settingsRepo = new SqliteSettingsRepository(context);

        var supplier = new Supplier("Distribuidora Andina", "ventas@andina.co", "Alimentos", SupplierStatus.Active);
        supplierRepo.Add(supplier);
        Assert.Equal(1, supplier.Id);
        Assert.Equal("PRV-01", supplier.Code);
        Assert.NotNull(supplierRepo.GetByCode("prv-01"));
        Assert.NotNull(supplierRepo.GetByName("distribuidora andina"));

        supplier.Update("Distribuidora Andina S.A.S.", "soporte@andina.co", "Bebidas", SupplierStatus.UnderReview, "PRV-10");
        supplierRepo.Update(supplier);
        var updatedSupplier = supplierRepo.GetById(supplier.Id);
        Assert.NotNull(updatedSupplier);
        Assert.Equal("PRV-10", updatedSupplier!.Code);
        Assert.Equal(SupplierStatus.UnderReview, updatedSupplier.Status);

        var p1 = new Product("Café Molido 500g", "CAF-500", "Bebidas", supplier.Id, 6.00m, 10.00m, minStock: 5);
        var p2 = new Product("Sal Marina 1kg", "SAL-001", "Alimentos", null, 0.80m, 1.50m, minStock: 3);
        productRepo.Add(p1);
        productRepo.Add(p2);

        Assert.NotNull(productRepo.GetBySku("caf-500"));
        Assert.Single(productRepo.GetBySupplierId(supplier.Id));

        p1.ConvertPrices(4000m, decimalPlaces: 0);
        p2.ConvertPrices(4000m, decimalPlaces: 0);
        productRepo.UpdateRange([p1, p2]);
        Assert.Equal(24000m, productRepo.GetById(p1.Id)!.PurchasePrice);
        Assert.Equal(6000m, productRepo.GetById(p2.Id)!.SalePrice);

        var explicitDate = new DateTime(2026, 11, 20, 14, 30, 15, DateTimeKind.Local);
        var movement = new StockMovement(p1.Id, 25, MovementType.Entry, "Compra lote #1", explicitDate);
        movementRepo.Add(movement);
        var loadedMovement = Assert.Single(movementRepo.GetByProductId(p1.Id));
        Assert.Equal(explicitDate, loadedMovement.Date);
        Assert.Equal(25, loadedMovement.Quantity);

        // Foreign keys must block deleting parents referenced by children
        Assert.Throws<SqliteException>(() => supplierRepo.Delete(supplier.Id));
        Assert.Throws<SqliteException>(() => productRepo.Delete(p1.Id));

        productRepo.Delete(p2.Id);
        Assert.Null(productRepo.GetById(p2.Id));

        var defaultSettings = settingsRepo.Get();
        Assert.Equal("USD", defaultSettings.CurrencyCode);

        var customSettings = new AppSettings("Bodega Central", "COP", 15);
        settingsRepo.Save(customSettings);
        var reloadedSettings = settingsRepo.Get();
        Assert.Equal("Bodega Central", reloadedSettings.BusinessName);
        Assert.Equal("COP", reloadedSettings.CurrencyCode);
        Assert.Equal(15, reloadedSettings.DefaultLowStockThreshold);
    }

    /// <summary>
    /// Verifies transactional JSON-to-SQLite migration (including legacy <c>Precio</c> field, filtered legacy transfers,
    /// settings preservation, stock balance reconciliation, backup file preservation, and idempotency on restart).
    /// </summary>
    [Fact]
    public void JsonToSqliteMigrator_MigratesLegacyJsonTransactionallyAndReconcilesBalances()
    {
        Directory.CreateDirectory(_tempDirectory);

        var suppliersJson = """
        [
          { "Id": 1, "Codigo": "", "Nombre": "Proveedor Uno", "Contacto": "uno@test.com", "CategoriaPrincipal": "Alimentos", "Estado": 0 }
        ]
        """;
        var productsJson = """
        [
          { "Id": 1, "Nombre": "Café Legado", "SKU": "CAF-LEG", "Categoria": "Bebidas", "ProveedorId": 1, "Precio": 12000, "StockMinimo": 5 },
          { "Id": 2, "Nombre": "Arroz Integral", "SKU": "ARR-002", "Categoria": "Alimentos", "ProveedorId": null, "PrecioCompra": 3000, "PrecioVenta": 4500, "StockMinimo": 10 }
        ]
        """;
        var ticketsJson = """
        [
          { "Id": 1, "Fecha": "2026-11-01T09:00:00", "ProductoId": 1, "Cantidad": 30, "Tipo": 0, "Observaciones": "Entrada inicial" },
          { "Id": 2, "Fecha": "2026-11-02T11:30:00", "ProductoId": 1, "Cantidad": 8, "Tipo": 1, "Observaciones": "Venta" },
          { "Id": 3, "Fecha": "2026-11-03T12:00:00", "ProductoId": 1, "Cantidad": 4, "Tipo": 2, "Observaciones": "Traslado legado" },
          { "Id": 4, "Fecha": "2026-11-04T15:00:00", "ProductoId": 2, "Cantidad": 15, "Tipo": 0, "Observaciones": "Compra arroz" }
        ]
        """;
        var settingsJson = """
        {
          "NombreNegocio": "Bodega Migrada",
          "MonedaPrincipal": "COP",
          "UmbralBajoStock": 8
        }
        """;

        File.WriteAllText(Path.Combine(_tempDirectory, "proveedores.json"), suppliersJson);
        File.WriteAllText(Path.Combine(_tempDirectory, "productos.json"), productsJson);
        File.WriteAllText(Path.Combine(_tempDirectory, "tickets.json"), ticketsJson);
        File.WriteAllText(Path.Combine(_tempDirectory, "settings.json"), settingsJson);

        var services = new ServiceCollection();
        services.AddTiendaBodegasServices(_tempDirectory);
        using var provider = services.BuildServiceProvider();

        DependencyInjection.InitializeDatabase(provider);

        var catalog = provider.GetRequiredService<ICatalogService>();
        var inventory = provider.GetRequiredService<IInventoryService>();
        var settings = provider.GetRequiredService<ISettingsService>().GetSettings();

        var migratedSupplier = Assert.Single(catalog.GetAllSuppliers());
        Assert.Equal("PRV-01", migratedSupplier.Code);

        var products = catalog.GetAllProducts().OrderBy(p => p.Id).ToList();
        Assert.Equal(2, products.Count);
        Assert.Equal(12000m, products[0].SalePrice);
        Assert.Equal(22, inventory.GetTotalStockByProduct(1));
        Assert.Equal(15, inventory.GetTotalStockByProduct(2));
        Assert.Equal(3, inventory.GetMovements().Count());

        Assert.Equal("Bodega Migrada", settings.BusinessName);
        Assert.Equal("COP", settings.CurrencyCode);
        Assert.Equal(8, settings.DefaultLowStockThreshold);

        // JSON backup files remain preserved on disk
        Assert.True(File.Exists(Path.Combine(_tempDirectory, "proveedores.json")));
        Assert.True(File.Exists(Path.Combine(_tempDirectory, "productos.json")));
        Assert.True(File.Exists(Path.Combine(_tempDirectory, "tickets.json")));
        Assert.True(File.Exists(Path.Combine(_tempDirectory, "settings.json")));

        // Subsequent inserts get sequential IDs after migrated max IDs
        var newProduct = catalog.CreateProduct("Té Verde", "TEA-003", "Bebidas", migratedSupplier.Id, 4000m, 7000m, 5);
        Assert.Equal(3, newProduct.Id);

        // Re-running migration when SQLite already has data is a no-op
        var sqliteContext = provider.GetRequiredService<SqliteDataContext>();
        var secondAttempt = JsonToSqliteMigrator.MigrateIfNeeded(_tempDirectory, sqliteContext);
        Assert.False(secondAttempt.Migrated);
    }

    /// <summary>
    /// Verifies that invalid JSON references abort migration atomically with zero partial rows written to SQLite.
    /// </summary>
    [Fact]
    public void JsonToSqliteMigrator_WhenReferenceIsInvalid_RollsBackWithoutPartialData()
    {
        Directory.CreateDirectory(_tempDirectory);

        File.WriteAllText(Path.Combine(_tempDirectory, "proveedores.json"), """
        [
          { "Id": 1, "Codigo": "PRV-01", "Nombre": "Proveedor Uno", "Contacto": "uno@test.com", "CategoriaPrincipal": "Alimentos", "Estado": 0 }
        ]
        """);
        File.WriteAllText(Path.Combine(_tempDirectory, "productos.json"), """
        [
          { "Id": 1, "Nombre": "Arroz 1kg", "SKU": "ARR-001", "Categoria": "Alimentos", "ProveedorId": 1, "PrecioCompra": 2.0, "PrecioVenta": 3.5, "StockMinimo": 5 }
        ]
        """);
        File.WriteAllText(Path.Combine(_tempDirectory, "tickets.json"), """
        [
          { "Id": 1, "Fecha": "2026-11-01T09:00:00", "ProductoId": 999, "Cantidad": 10, "Tipo": 0, "Observaciones": "Referencia rota" }
        ]
        """);

        using var context = new SqliteDataContext(_tempDirectory);
        Assert.Throws<InvalidOperationException>(() =>
            JsonToSqliteMigrator.MigrateIfNeeded(_tempDirectory, context));

        Assert.Empty(new SqliteSupplierRepository(context).GetAll());
        Assert.Empty(new SqliteProductRepository(context).GetAll());
        Assert.Empty(new SqliteStockMovementRepository(context).GetAll());
        Assert.True(File.Exists(Path.Combine(_tempDirectory, "tickets.json")));
    }

    /// <summary>
    /// Cleans up the temporary directory after each test.
    /// </summary>
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }
}

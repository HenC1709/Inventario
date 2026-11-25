using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Enums;
using TiendaBodegas.Infrastructure.Persistence.Json;

namespace TiendaBodegas.Infrastructure.Persistence.Sqlite;

/// <summary>
/// Summary of a JSON-to-SQLite migration attempt, including entity counts and balance reconciliation status.
/// </summary>
public sealed record JsonMigrationResult(
    bool Migrated,
    int SuppliersMigrated,
    int ProductsMigrated,
    int MovementsMigrated,
    bool SettingsMigrated,
    bool BalancesReconciled);

/// <summary>
/// Transactional, idempotent migrator importing legacy JSON files (<c>proveedores.json</c>, <c>productos.json</c>,
/// <c>tickets.json</c>, <c>settings.json</c>) into SQLite while preserving JSON backups.
/// </summary>
public static class JsonToSqliteMigrator
{
    /// <summary>
    /// Migrates JSON data into SQLite once when the SQLite database is empty and JSON files exist.
    /// </summary>
    public static JsonMigrationResult MigrateIfNeeded(string dataDirectory, SqliteDataContext sqliteContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentNullException.ThrowIfNull(sqliteContext);

        sqliteContext.InitializeSchema();

        using var connection = sqliteContext.CreateOpenConnection();
        if (DatabaseHasAnyData(connection))
            return new JsonMigrationResult(false, 0, 0, 0, false, true);

        var suppliersPath = Path.Combine(dataDirectory, "proveedores.json");
        var productsPath = Path.Combine(dataDirectory, "productos.json");
        var movementsPath = Path.Combine(dataDirectory, "tickets.json");
        var settingsPath = Path.Combine(dataDirectory, "settings.json");

        var hasAnyJsonFile = File.Exists(suppliersPath)
            || File.Exists(productsPath)
            || File.Exists(movementsPath)
            || File.Exists(settingsPath);

        if (!hasAnyJsonFile)
            return new JsonMigrationResult(false, 0, 0, 0, false, true);

        var jsonContext = new JsonDataContext(dataDirectory);
        jsonContext.LoadJson();

        ValidateSourceIntegrity(jsonContext);

        var shouldMigrateSettings = File.Exists(settingsPath)
            || jsonContext.Suppliers.Count > 0
            || jsonContext.Products.Count > 0
            || jsonContext.StockMovements.Count > 0;

        using var transaction = connection.BeginTransaction();

        InsertSuppliers(connection, transaction, jsonContext.Suppliers);
        InsertProducts(connection, transaction, jsonContext.Products);
        InsertMovements(connection, transaction, jsonContext.StockMovements);

        if (shouldMigrateSettings)
            InsertSettings(connection, transaction, jsonContext.Settings);

        ReconcileCountsAndBalances(connection, transaction, jsonContext);

        transaction.Commit();

        return new JsonMigrationResult(
            Migrated: true,
            SuppliersMigrated: jsonContext.Suppliers.Count,
            ProductsMigrated: jsonContext.Products.Count,
            MovementsMigrated: jsonContext.StockMovements.Count,
            SettingsMigrated: shouldMigrateSettings,
            BalancesReconciled: true);
    }

    private static bool DatabaseHasAnyData(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE
                WHEN EXISTS(SELECT 1 FROM Suppliers)
                  OR EXISTS(SELECT 1 FROM Products)
                  OR EXISTS(SELECT 1 FROM StockMovements)
                  OR EXISTS(SELECT 1 FROM Settings)
                THEN 1 ELSE 0 END;
            """;
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
    }

    private static void ValidateSourceIntegrity(JsonDataContext jsonContext)
    {
        var supplierIds = new HashSet<int>();
        var supplierCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var supplier in jsonContext.Suppliers)
        {
            if (supplier.Id <= 0)
                throw new InvalidOperationException($"Proveedor con identificador inválido ({supplier.Id}) durante la migración JSON.");

            if (!supplierIds.Add(supplier.Id))
                throw new InvalidOperationException($"Identificador de proveedor duplicado ({supplier.Id}) en proveedores.json.");

            var resolvedCode = string.IsNullOrWhiteSpace(supplier.Code) ? $"PRV-{supplier.Id:D2}" : supplier.Code.Trim();
            if (!supplierCodes.Add(resolvedCode))
                throw new InvalidOperationException($"Código de proveedor duplicado ({resolvedCode}) en proveedores.json.");
        }

        var productIds = new HashSet<int>();
        var productSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var product in jsonContext.Products)
        {
            if (product.Id <= 0)
                throw new InvalidOperationException($"Producto con identificador inválido ({product.Id}) durante la migración JSON.");

            if (!productIds.Add(product.Id))
                throw new InvalidOperationException($"Identificador de producto duplicado ({product.Id}) en productos.json.");

            if (string.IsNullOrWhiteSpace(product.SKU))
                throw new InvalidOperationException($"Producto #{product.Id} sin SKU válido en productos.json.");

            if (!productSkus.Add(product.SKU.Trim()))
                throw new InvalidOperationException($"SKU duplicado ({product.SKU}) en productos.json.");

            if (product.SupplierId.HasValue && !supplierIds.Contains(product.SupplierId.Value))
                throw new InvalidOperationException($"El producto #{product.Id} ({product.SKU}) referencia al proveedor inexistente #{product.SupplierId.Value}.");
        }

        var movementIds = new HashSet<int>();
        foreach (var movement in jsonContext.StockMovements)
        {
            if (movement.Id <= 0)
                throw new InvalidOperationException($"Movimiento con identificador inválido ({movement.Id}) durante la migración JSON.");

            if (!movementIds.Add(movement.Id))
                throw new InvalidOperationException($"Identificador de movimiento duplicado ({movement.Id}) en tickets.json.");

            if (!productIds.Contains(movement.ProductId))
                throw new InvalidOperationException($"El movimiento #{movement.Id} referencia al producto inexistente #{movement.ProductId}.");

            if (movement.Quantity <= 0)
                throw new InvalidOperationException($"El movimiento #{movement.Id} tiene una cantidad inválida ({movement.Quantity}).");
        }
    }

    private static void InsertSuppliers(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<Supplier> suppliers)
    {
        foreach (var supplier in suppliers)
        {
            var resolvedCode = string.IsNullOrWhiteSpace(supplier.Code)
                ? $"PRV-{supplier.Id:D2}"
                : supplier.Code.Trim();

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Suppliers (Id, Code, Name, Contact, MainCategory, Status)
                VALUES (@Id, @Code, @Name, @Contact, @MainCategory, @Status);
                """;
            command.Parameters.AddWithValue("@Id", supplier.Id);
            command.Parameters.AddWithValue("@Code", resolvedCode);
            command.Parameters.AddWithValue("@Name", supplier.Name);
            command.Parameters.AddWithValue("@Contact", supplier.Contact);
            command.Parameters.AddWithValue("@MainCategory", supplier.MainCategory);
            command.Parameters.AddWithValue("@Status", (int)supplier.Status);
            command.ExecuteNonQuery();
        }
    }

    private static void InsertProducts(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<Product> products)
    {
        foreach (var product in products)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Products (Id, Name, SKU, Category, SupplierId, PurchasePrice, SalePrice, MinStock)
                VALUES (@Id, @Name, @Sku, @Category, @SupplierId, @PurchasePrice, @SalePrice, @MinStock);
                """;
            command.Parameters.AddWithValue("@Id", product.Id);
            command.Parameters.AddWithValue("@Name", product.Name);
            command.Parameters.AddWithValue("@Sku", product.SKU);
            command.Parameters.AddWithValue("@Category", product.Category);
            command.Parameters.AddWithValue("@SupplierId", product.SupplierId.HasValue ? product.SupplierId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@PurchasePrice", product.PurchasePrice.ToString(CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@SalePrice", product.SalePrice.ToString(CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@MinStock", product.MinStock);
            command.ExecuteNonQuery();
        }
    }

    private static void InsertMovements(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<StockMovement> movements)
    {
        foreach (var movement in movements)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO StockMovements (Id, Date, ProductId, Quantity, MovementType, Notes)
                VALUES (@Id, @Date, @ProductId, @Quantity, @MovementType, @Notes);
                """;
            command.Parameters.AddWithValue("@Id", movement.Id);
            command.Parameters.AddWithValue("@Date", movement.Date.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@ProductId", movement.ProductId);
            command.Parameters.AddWithValue("@Quantity", movement.Quantity);
            command.Parameters.AddWithValue("@MovementType", (int)movement.MovementType);
            command.Parameters.AddWithValue("@Notes", movement.Notes ?? string.Empty);
            command.ExecuteNonQuery();
        }
    }

    private static void InsertSettings(SqliteConnection connection, SqliteTransaction transaction, AppSettings settings)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Settings (Id, BusinessName, CurrencyCode, DefaultLowStockThreshold)
            VALUES (1, @BusinessName, @CurrencyCode, @Threshold);
            """;
        command.Parameters.AddWithValue("@BusinessName", settings.BusinessName);
        command.Parameters.AddWithValue("@CurrencyCode", settings.CurrencyCode);
        command.Parameters.AddWithValue("@Threshold", settings.DefaultLowStockThreshold);
        command.ExecuteNonQuery();
    }

    private static void ReconcileCountsAndBalances(
        SqliteConnection connection,
        SqliteTransaction transaction,
        JsonDataContext jsonContext)
    {
        var supplierCount = ExecuteCount(connection, transaction, "Suppliers");
        var productCount = ExecuteCount(connection, transaction, "Products");
        var movementCount = ExecuteCount(connection, transaction, "StockMovements");

        if (supplierCount != jsonContext.Suppliers.Count
            || productCount != jsonContext.Products.Count
            || movementCount != jsonContext.StockMovements.Count)
        {
            throw new InvalidOperationException("La conciliación de conteos entre JSON y SQLite falló; se revierte la migración.");
        }

        var expectedBalances = jsonContext.Products.ToDictionary(p => p.Id, _ => 0);
        foreach (var movement in jsonContext.StockMovements)
        {
            var delta = movement.MovementType == MovementType.Entry ? movement.Quantity : -movement.Quantity;
            expectedBalances[movement.ProductId] = expectedBalances[movement.ProductId] + delta;
        }

        var actualBalances = new Dictionary<int, int>();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                SELECT p.Id,
                       COALESCE(SUM(CASE
                           WHEN m.MovementType = 0 THEN m.Quantity
                           WHEN m.MovementType = 1 THEN -m.Quantity
                           ELSE 0
                       END), 0) AS NetStock
                FROM Products p
                LEFT JOIN StockMovements m ON m.ProductId = p.Id
                GROUP BY p.Id;
                """;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                actualBalances[reader.GetInt32(0)] = reader.GetInt32(1);
            }
        }

        foreach (var (productId, expectedStock) in expectedBalances)
        {
            if (!actualBalances.TryGetValue(productId, out var actualStock) || actualStock != expectedStock)
            {
                throw new InvalidOperationException(
                    $"La conciliación de saldo falló para el producto #{productId}: esperado {expectedStock}, obtenido {actualStock}.");
            }
        }
    }

    private static int ExecuteCount(SqliteConnection connection, SqliteTransaction transaction, string tableName)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
}

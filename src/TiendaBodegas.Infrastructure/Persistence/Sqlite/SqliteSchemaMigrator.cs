using System.Globalization;
using Microsoft.Data.Sqlite;

namespace TiendaBodegas.Infrastructure.Persistence.Sqlite;

/// <summary>
/// Manages versioned SQLite schema migrations using the <c>SchemaVersions</c> tracking table.
/// </summary>
public static class SqliteSchemaMigrator
{
    private sealed record SchemaMigration(int Version, string Description, string Sql);

    private static readonly IReadOnlyList<SchemaMigration> Migrations =
    [
        new SchemaMigration(
            Version: 1,
            Description: "Initial relational schema for Suppliers, Products, StockMovements, and Settings",
            Sql: """
                CREATE TABLE IF NOT EXISTS Suppliers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Code TEXT NOT NULL COLLATE NOCASE UNIQUE,
                    Name TEXT NOT NULL COLLATE NOCASE,
                    Contact TEXT NOT NULL,
                    MainCategory TEXT NOT NULL,
                    Status INTEGER NOT NULL CHECK (Status IN (0, 1))
                );

                CREATE INDEX IF NOT EXISTS IX_Suppliers_Name
                    ON Suppliers (Name);

                CREATE TABLE IF NOT EXISTS Products (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    SKU TEXT NOT NULL COLLATE NOCASE UNIQUE,
                    Category TEXT NOT NULL,
                    SupplierId INTEGER NULL,
                    PurchasePrice TEXT NOT NULL,
                    SalePrice TEXT NOT NULL,
                    MinStock INTEGER NOT NULL DEFAULT 0 CHECK (MinStock >= 0),
                    FOREIGN KEY (SupplierId) REFERENCES Suppliers (Id) ON DELETE RESTRICT
                );

                CREATE INDEX IF NOT EXISTS IX_Products_SupplierId
                    ON Products (SupplierId);

                CREATE INDEX IF NOT EXISTS IX_Products_Category
                    ON Products (Category);

                CREATE TABLE IF NOT EXISTS StockMovements (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Date TEXT NOT NULL,
                    ProductId INTEGER NOT NULL,
                    Quantity INTEGER NOT NULL CHECK (Quantity > 0),
                    MovementType INTEGER NOT NULL CHECK (MovementType IN (0, 1)),
                    Notes TEXT NOT NULL DEFAULT '',
                    FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE RESTRICT
                );

                CREATE INDEX IF NOT EXISTS IX_StockMovements_ProductId
                    ON StockMovements (ProductId);

                CREATE INDEX IF NOT EXISTS IX_StockMovements_Date
                    ON StockMovements (Date);

                CREATE INDEX IF NOT EXISTS IX_StockMovements_MovementType
                    ON StockMovements (MovementType);

                CREATE TABLE IF NOT EXISTS Settings (
                    Id INTEGER PRIMARY KEY CHECK (Id = 1),
                    BusinessName TEXT NOT NULL,
                    CurrencyCode TEXT NOT NULL,
                    DefaultLowStockThreshold INTEGER NOT NULL CHECK (DefaultLowStockThreshold >= 0)
                );
                """)
    ];

    /// <summary>
    /// Creates the <c>SchemaVersions</c> table if needed and applies all pending schema migrations transactionally.
    /// </summary>
    public static void Migrate(SqliteDataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var connection = context.CreateRawOpenConnection();
        EnsureVersionTable(connection);

        var appliedVersions = ReadAppliedVersions(connection).ToHashSet();

        foreach (var migration in Migrations.OrderBy(m => m.Version))
        {
            if (appliedVersions.Contains(migration.Version))
                continue;

            using var transaction = connection.BeginTransaction();

            using (var sqlCommand = connection.CreateCommand())
            {
                sqlCommand.Transaction = transaction;
                sqlCommand.CommandText = migration.Sql;
                sqlCommand.ExecuteNonQuery();
            }

            using (var versionCommand = connection.CreateCommand())
            {
                versionCommand.Transaction = transaction;
                versionCommand.CommandText = """
                    INSERT INTO SchemaVersions (Version, Description, AppliedAtUtc)
                    VALUES (@Version, @Description, @AppliedAtUtc);
                    """;
                versionCommand.Parameters.AddWithValue("@Version", migration.Version);
                versionCommand.Parameters.AddWithValue("@Description", migration.Description);
                versionCommand.Parameters.AddWithValue("@AppliedAtUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                versionCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    /// <summary>
    /// Returns the ordered list of schema migration version numbers currently applied in the database.
    /// </summary>
    public static IReadOnlyList<int> GetAppliedVersions(SqliteDataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var connection = context.CreateRawOpenConnection();
        EnsureVersionTable(connection);
        return ReadAppliedVersions(connection);
    }

    private static void EnsureVersionTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS SchemaVersions (
                Version INTEGER PRIMARY KEY,
                Description TEXT NOT NULL,
                AppliedAtUtc TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private static List<int> ReadAppliedVersions(SqliteConnection connection)
    {
        var versions = new List<int>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Version FROM SchemaVersions ORDER BY Version;";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            versions.Add(reader.GetInt32(0));
        }

        return versions;
    }
}

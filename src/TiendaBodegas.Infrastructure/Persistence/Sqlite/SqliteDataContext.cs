using System.IO;
using Microsoft.Data.Sqlite;

namespace TiendaBodegas.Infrastructure.Persistence.Sqlite;

/// <summary>
/// SQLite-backed data context managing the local database path, connection lifecycle, and schema initialization.
/// </summary>
public class SqliteDataContext : IDisposable
{
    private readonly object _schemaLock = new();
    private bool _schemaInitialized;
    private bool _disposed;

    /// <summary>
    /// Gets the directory containing the local SQLite database file.
    /// </summary>
    public string DataDirectory { get; }

    /// <summary>
    /// Gets the full path to the <c>tiendabodegas.db</c> SQLite database file.
    /// </summary>
    public string DatabasePath { get; }

    /// <summary>
    /// Gets the SQLite connection string configured with pooling disabled to prevent file locking on Windows.
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// Initializes a new SQLite data context targeting the given directory, or the default application data folder.
    /// </summary>
    public SqliteDataContext(string? dataDirectory = null)
    {
        DataDirectory = dataDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        Directory.CreateDirectory(DataDirectory);

        DatabasePath = Path.Combine(DataDirectory, "tiendabodegas.db");
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();
    }

    /// <summary>
    /// Ensures that the versioned relational schema has been created and migrated to the latest version.
    /// </summary>
    public void InitializeSchema()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_schemaInitialized)
            return;

        lock (_schemaLock)
        {
            if (_schemaInitialized)
                return;

            SqliteSchemaMigrator.Migrate(this);
            _schemaInitialized = true;
        }
    }

    /// <summary>
    /// Ensures schema initialization and returns an open <see cref="SqliteConnection"/> with foreign keys enabled.
    /// </summary>
    public SqliteConnection CreateOpenConnection()
    {
        InitializeSchema();
        return CreateRawOpenConnection();
    }

    /// <summary>
    /// Opens a <see cref="SqliteConnection"/> with foreign keys and busy timeout configured without triggering recursive schema checks.
    /// </summary>
    internal SqliteConnection CreateRawOpenConnection()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
        pragmaCommand.ExecuteNonQuery();

        return connection;
    }

    /// <summary>
    /// Clears any residual SQLite connection pools and marks the context as disposed.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        SqliteConnection.ClearAllPools();
        GC.SuppressFinalize(this);
    }
}

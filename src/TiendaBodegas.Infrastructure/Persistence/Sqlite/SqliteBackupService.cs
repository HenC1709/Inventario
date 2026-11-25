using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;
using TiendaBodegas.Application.Interfaces;

namespace TiendaBodegas.Infrastructure.Persistence.Sqlite;

/// <summary>
/// SQLite implementation of <see cref="IDatabaseBackupService"/> using SQLite's online backup API,
/// integrity checks, schema validation, and pre-restore safety copies.
/// </summary>
public class SqliteBackupService : IDatabaseBackupService
{
    private const int MinimumSqliteFileSizeBytes = 100;
    private const int RetainedPreRestoreCopies = 5;

    private static readonly byte[] SqliteHeaderMagic = Encoding.ASCII.GetBytes("SQLite format 3\0");
    private static readonly string[] RequiredTables = ["Suppliers", "Products", "StockMovements", "Settings"];

    private readonly SqliteDataContext _context;
    private readonly IAppLogger? _logger;

    /// <inheritdoc />
    public string ActiveDataDirectory => _context.DataDirectory;

    /// <inheritdoc />
    public string ActiveDatabasePath => _context.DatabasePath;

    /// <inheritdoc />
    public event Action? DatabaseRestored;

    /// <summary>
    /// Initializes a new instance of <see cref="SqliteBackupService"/>.
    /// </summary>
    public SqliteBackupService(SqliteDataContext context, IAppLogger? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
    }

    /// <inheritdoc />
    public void CreateBackup(string destinationFilePath)
    {
        if (string.IsNullOrWhiteSpace(destinationFilePath))
            throw new ArgumentException("La ruta de destino para la copia de seguridad es obligatoria.", nameof(destinationFilePath));

        var fullDestination = Path.GetFullPath(destinationFilePath);
        var fullActiveDb = Path.GetFullPath(_context.DatabasePath);

        if (string.Equals(fullDestination, fullActiveDb, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("No se puede sobrescribir el archivo de base de datos activo como destino del respaldo.");

        var destinationDirectory = Path.GetDirectoryName(fullDestination);
        if (!string.IsNullOrEmpty(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        var tempDestination = fullDestination + ".tmp";
        if (File.Exists(tempDestination))
            File.Delete(tempDestination);

        try
        {
            using (var sourceConnection = _context.CreateOpenConnection())
            using (var destinationConnection = new SqliteConnection(BuildConnectionString(tempDestination, SqliteOpenMode.ReadWriteCreate)))
            {
                destinationConnection.Open();
                sourceConnection.BackupDatabase(destinationConnection);

                using var checkCommand = destinationConnection.CreateCommand();
                checkCommand.CommandText = "PRAGMA integrity_check;";
                var result = checkCommand.ExecuteScalar()?.ToString();
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("La verificación de integridad de la copia de seguridad falló.");
            }

            SqliteConnection.ClearAllPools();
            File.Move(tempDestination, fullDestination, overwrite: true);
            _logger?.Info($"Copia de seguridad creada en {fullDestination}.");
        }
        catch (Exception ex)
        {
            _logger?.Error($"Error al crear la copia de seguridad en {fullDestination}.", ex);
            throw;
        }
        finally
        {
            if (File.Exists(tempDestination))
            {
                try
                {
                    File.Delete(tempDestination);
                }
                catch
                {
                    // Ignore best-effort temporary file cleanup failures.
                }
            }
        }
    }

    /// <inheritdoc />
    public void RestoreBackup(string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
            throw new ArgumentException("La ruta del archivo de respaldo es obligatoria.", nameof(sourceFilePath));

        var fullSource = Path.GetFullPath(sourceFilePath);
        if (!File.Exists(fullSource))
            throw new FileNotFoundException("El archivo de copia de seguridad seleccionado no existe.", fullSource);

        var fullActiveDb = Path.GetFullPath(_context.DatabasePath);
        if (string.Equals(fullSource, fullActiveDb, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("No se puede restaurar la base de datos desde el archivo activo.");

        ValidateBackupCandidate(fullSource);

        if (File.Exists(_context.DatabasePath))
        {
            var backupsDir = Path.Combine(_context.DataDirectory, "backups");
            Directory.CreateDirectory(backupsDir);
            var preRestorePath = Path.Combine(backupsDir, $"pre-restore-{DateTime.Now:yyyyMMdd-HHmmss-fff}.db");
            CreateBackup(preRestorePath);
            PruneOldPreRestoreBackups(backupsDir);
        }

        try
        {
            using (var sourceConnection = new SqliteConnection(BuildConnectionString(fullSource, SqliteOpenMode.ReadOnly)))
            using (var liveConnection = _context.CreateOpenConnection())
            {
                sourceConnection.Open();
                sourceConnection.BackupDatabase(liveConnection);
            }

            SqliteConnection.ClearAllPools();
            SqliteSchemaMigrator.Migrate(_context);
            _logger?.Info($"Base de datos restaurada correctamente desde {fullSource}.");
            DatabaseRestored?.Invoke();
        }
        catch (Exception ex)
        {
            _logger?.Error($"Error al restaurar la copia de seguridad desde {fullSource}.", ex);
            throw;
        }
    }

    private static void ValidateBackupCandidate(string candidatePath)
    {
        var fileInfo = new FileInfo(candidatePath);
        if (fileInfo.Length < MinimumSqliteFileSizeBytes)
            throw new InvalidOperationException("El archivo seleccionado no es una copia de seguridad SQLite válida de StockFlow.");

        var header = new byte[SqliteHeaderMagic.Length];
        using (var stream = File.OpenRead(candidatePath))
        {
            var bytesRead = stream.Read(header, 0, header.Length);
            if (bytesRead < SqliteHeaderMagic.Length || !header.SequenceEqual(SqliteHeaderMagic))
                throw new InvalidOperationException("El archivo seleccionado no es una copia de seguridad SQLite válida de StockFlow.");
        }

        try
        {
            using var connection = new SqliteConnection(BuildConnectionString(candidatePath, SqliteOpenMode.ReadOnly));
            connection.Open();

            using (var checkCommand = connection.CreateCommand())
            {
                checkCommand.CommandText = "PRAGMA quick_check;";
                var checkResult = checkCommand.ExecuteScalar()?.ToString();
                if (!string.Equals(checkResult, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("El archivo de copia de seguridad está dañado o incompleto.");
            }

            var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var tablesCommand = connection.CreateCommand())
            {
                tablesCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
                using var reader = tablesCommand.ExecuteReader();
                while (reader.Read())
                {
                    existingTables.Add(reader.GetString(0));
                }
            }

            foreach (var requiredTable in RequiredTables)
            {
                if (!existingTables.Contains(requiredTable))
                    throw new InvalidOperationException("El archivo seleccionado no contiene un esquema válido de StockFlow.");
            }
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException("El archivo seleccionado no es una base de datos SQLite válida de StockFlow.", ex);
        }
    }

    private static void PruneOldPreRestoreBackups(string backupsDir)
    {
        var preRestoreFiles = new DirectoryInfo(backupsDir)
            .GetFiles("pre-restore-*.db")
            .OrderByDescending(file => file.CreationTimeUtc)
            .ThenByDescending(file => file.Name, StringComparer.Ordinal)
            .Skip(RetainedPreRestoreCopies)
            .ToList();

        foreach (var oldFile in preRestoreFiles)
        {
            try
            {
                oldFile.Delete();
            }
            catch
            {
                // Ignore best-effort cleanup of old safety backups.
            }
        }
    }

    private static string BuildConnectionString(string path, SqliteOpenMode mode)
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Pooling = false
        }.ToString();
    }
}

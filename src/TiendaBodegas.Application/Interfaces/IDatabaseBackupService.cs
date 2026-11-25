namespace TiendaBodegas.Application.Interfaces;

/// <summary>
/// Application service contract for creating consistent SQLite database backups and restoring validated snapshots.
/// </summary>
public interface IDatabaseBackupService
{
    /// <summary>
    /// Gets the active user data directory hosting the database, backups, and diagnostic logs.
    /// </summary>
    string ActiveDataDirectory { get; }

    /// <summary>
    /// Gets the full file path of the active SQLite database.
    /// </summary>
    string ActiveDatabasePath { get; }

    /// <summary>
    /// Raised after a database backup has been restored successfully so UI ViewModels can reload live state.
    /// </summary>
    event Action? DatabaseRestored;

    /// <summary>
    /// Creates a consistent, integrity-checked backup copy of the active database at <paramref name="destinationFilePath"/>.
    /// </summary>
    void CreateBackup(string destinationFilePath);

    /// <summary>
    /// Validates and restores a SQLite backup file over the active database after saving a pre-restore safety copy.
    /// </summary>
    void RestoreBackup(string sourceFilePath);
}

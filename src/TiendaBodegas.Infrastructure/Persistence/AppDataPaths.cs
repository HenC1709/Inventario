using System.IO;

namespace TiendaBodegas.Infrastructure.Persistence;

/// <summary>
/// Resolves per-user application storage directories under <c>%LOCALAPPDATA%\StockFlow</c>
/// and safely promotes legacy local data when initializing an empty user store.
/// </summary>
public static class AppDataPaths
{
    /// <summary>
    /// Name of the application data folder under the user's LocalApplicationData directory.
    /// </summary>
    public const string AppFolderName = "StockFlow";

    private static readonly string[] KnownDataFiles =
    [
        "tiendabodegas.db",
        "proveedores.json",
        "productos.json",
        "tickets.json",
        "settings.json"
    ];

    /// <summary>
    /// Returns the default per-user storage directory (<c>%LOCALAPPDATA%\StockFlow</c>),
    /// falling back to <c>AppBase/data</c> if LocalApplicationData is unavailable.
    /// </summary>
    public static string GetDefaultDataDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data")
            : Path.Combine(localAppData, AppFolderName);
    }

    /// <summary>
    /// Resolves the active data directory. Explicit test/custom directories are created as-is without
    /// touching <c>%LOCALAPPDATA%</c>; otherwise the per-user folder is resolved and legacy files are promoted if empty.
    /// </summary>
    public static string ResolveDataDirectory(string? customDataDirectory = null, string? legacyBaseDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(customDataDirectory))
        {
            Directory.CreateDirectory(customDataDirectory);
            return customDataDirectory;
        }

        var targetDirectory = GetDefaultDataDirectory();
        Directory.CreateDirectory(targetDirectory);

        var candidates = new List<string>
        {
            legacyBaseDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data")
        };

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
            candidates.Add(Path.Combine(localAppData, "TiendaBodegas"));

        PromoteLegacyDataIfTargetEmpty(targetDirectory, candidates.ToArray());
        return targetDirectory;
    }

    /// <summary>
    /// Copies existing database or JSON files from the first non-empty legacy candidate directory into
    /// <paramref name="targetDirectory"/> when the target directory contains no database or JSON files yet.
    /// Original files are preserved intact as backups.
    /// </summary>
    public static bool PromoteLegacyDataIfTargetEmpty(string targetDirectory, params string[] candidateLegacyDirectories)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
        Directory.CreateDirectory(targetDirectory);

        if (ContainsAnyKnownDataFile(targetDirectory))
            return false;

        var normalizedTarget = Path.GetFullPath(targetDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (var candidate in candidateLegacyDirectories)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !Directory.Exists(candidate))
                continue;

            var normalizedCandidate = Path.GetFullPath(candidate)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(normalizedCandidate, normalizedTarget, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!ContainsAnyKnownDataFile(candidate))
                continue;

            var copiedAny = false;
            foreach (var fileName in KnownDataFiles)
            {
                var sourcePath = Path.Combine(candidate, fileName);
                var targetPath = Path.Combine(targetDirectory, fileName);
                if (File.Exists(sourcePath) && !File.Exists(targetPath))
                {
                    File.Copy(sourcePath, targetPath, overwrite: false);
                    copiedAny = true;
                }
            }

            if (copiedAny)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the diagnostic logs subdirectory under the active data folder.
    /// </summary>
    public static string GetLogsDirectory(string? dataDirectory = null)
    {
        return Path.Combine(dataDirectory ?? GetDefaultDataDirectory(), "logs");
    }

    private static bool ContainsAnyKnownDataFile(string directory)
    {
        return KnownDataFiles.Any(fileName => File.Exists(Path.Combine(directory, fileName)));
    }
}

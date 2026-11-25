using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Interfaces;

namespace TiendaBodegas.Infrastructure.Persistence.Sqlite.Repositories;

/// <summary>
/// SQLite-backed repository implementation for singleton <see cref="AppSettings"/> preferences.
/// </summary>
public class SqliteSettingsRepository : ISettingsRepository
{
    private readonly SqliteDataContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="SqliteSettingsRepository"/>.
    /// </summary>
    public SqliteSettingsRepository(SqliteDataContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public AppSettings Get()
    {
        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT BusinessName, CurrencyCode, DefaultLowStockThreshold
            FROM Settings
            WHERE Id = 1;
            """;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return new AppSettings();

        return new AppSettings(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt32(2));
    }

    /// <inheritdoc />
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Settings (Id, BusinessName, CurrencyCode, DefaultLowStockThreshold)
            VALUES (1, @BusinessName, @CurrencyCode, @DefaultLowStockThreshold)
            ON CONFLICT(Id) DO UPDATE SET
                BusinessName = excluded.BusinessName,
                CurrencyCode = excluded.CurrencyCode,
                DefaultLowStockThreshold = excluded.DefaultLowStockThreshold;
            """;
        command.Parameters.AddWithValue("@BusinessName", settings.BusinessName);
        command.Parameters.AddWithValue("@CurrencyCode", settings.CurrencyCode);
        command.Parameters.AddWithValue("@DefaultLowStockThreshold", settings.DefaultLowStockThreshold);
        command.ExecuteNonQuery();
    }
}

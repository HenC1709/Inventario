using System.Globalization;
using Microsoft.Data.Sqlite;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Enums;
using TiendaBodegas.Domain.Interfaces;

namespace TiendaBodegas.Infrastructure.Persistence.Sqlite.Repositories;

/// <summary>
/// SQLite-backed repository implementation for <see cref="StockMovement"/> entities.
/// </summary>
public class SqliteStockMovementRepository : IStockMovementRepository
{
    private readonly SqliteDataContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="SqliteStockMovementRepository"/>.
    /// </summary>
    public SqliteStockMovementRepository(SqliteDataContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public IEnumerable<StockMovement> GetAll()
    {
        var movements = new List<StockMovement>();
        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Date, ProductId, Quantity, MovementType, Notes
            FROM StockMovements
            ORDER BY Id;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            movements.Add(MapMovement(reader));
        }

        return movements;
    }

    /// <inheritdoc />
    public StockMovement? GetById(int id)
    {
        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Date, ProductId, Quantity, MovementType, Notes
            FROM StockMovements
            WHERE Id = @Id;
            """;
        command.Parameters.AddWithValue("@Id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapMovement(reader) : null;
    }

    /// <inheritdoc />
    public IEnumerable<StockMovement> GetByProductId(int productId)
    {
        var movements = new List<StockMovement>();
        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Date, ProductId, Quantity, MovementType, Notes
            FROM StockMovements
            WHERE ProductId = @ProductId
            ORDER BY Id;
            """;
        command.Parameters.AddWithValue("@ProductId", productId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            movements.Add(MapMovement(reader));
        }

        return movements;
    }

    /// <inheritdoc />
    public IEnumerable<StockMovement> GetByMovementType(MovementType movementType)
    {
        var movements = new List<StockMovement>();
        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Date, ProductId, Quantity, MovementType, Notes
            FROM StockMovements
            WHERE MovementType = @MovementType
            ORDER BY Id;
            """;
        command.Parameters.AddWithValue("@MovementType", (int)movementType);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            movements.Add(MapMovement(reader));
        }

        return movements;
    }

    /// <inheritdoc />
    public void Add(StockMovement movement)
    {
        ArgumentNullException.ThrowIfNull(movement);

        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO StockMovements (Date, ProductId, Quantity, MovementType, Notes)
            VALUES (@Date, @ProductId, @Quantity, @MovementType, @Notes);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("@Date", movement.Date.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@ProductId", movement.ProductId);
        command.Parameters.AddWithValue("@Quantity", movement.Quantity);
        command.Parameters.AddWithValue("@MovementType", (int)movement.MovementType);
        command.Parameters.AddWithValue("@Notes", movement.Notes ?? string.Empty);

        var newId = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        movement.AssignId(newId);
    }

    private static StockMovement MapMovement(SqliteDataReader reader)
    {
        var id = reader.GetInt32(0);
        var date = DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var productId = reader.GetInt32(2);
        var quantity = reader.GetInt32(3);
        var movementType = (MovementType)reader.GetInt32(4);
        var notes = reader.GetString(5);

        var movement = new StockMovement(productId, quantity, movementType, notes, date);
        movement.AssignId(id);
        return movement;
    }
}

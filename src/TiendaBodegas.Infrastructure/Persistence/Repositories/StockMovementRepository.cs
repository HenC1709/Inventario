using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Enums;
using TiendaBodegas.Domain.Interfaces;
using TiendaBodegas.Infrastructure.Persistence.Json;

namespace TiendaBodegas.Infrastructure.Persistence.Repositories;

/// <summary>
/// JSON-backed repository implementation for <see cref="StockMovement"/> entities.
/// </summary>
public class StockMovementRepository : IStockMovementRepository
{
    private readonly JsonDataContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="StockMovementRepository"/>.
    /// </summary>
    public StockMovementRepository(JsonDataContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves all persisted stock movements.
    /// </summary>
    public IEnumerable<StockMovement> GetAll()
    {
        return _context.StockMovements;
    }

    /// <summary>
    /// Finds a stock movement by its unique identifier.
    /// </summary>
    public StockMovement? GetById(int id)
    {
        return _context.StockMovements.FirstOrDefault(movement => movement.Id == id);
    }

    /// <summary>
    /// Retrieves all movements associated with the specified product identifier.
    /// </summary>
    public IEnumerable<StockMovement> GetByProductId(int productId)
    {
        return _context.StockMovements.Where(movement => movement.ProductId == productId);
    }

    /// <summary>
    /// Retrieves all movements matching the specified movement type.
    /// </summary>
    public IEnumerable<StockMovement> GetByMovementType(MovementType movementType)
    {
        return _context.StockMovements.Where(movement => movement.MovementType == movementType);
    }

    /// <summary>
    /// Assigns the next sequential identifier, adds the stock movement, and persists changes to JSON.
    /// </summary>
    public void Add(StockMovement movement)
    {
        var nextId = _context.StockMovements.Any()
            ? _context.StockMovements.Max(item => item.Id) + 1
            : 1;
        movement.AssignId(nextId);
        _context.StockMovements.Add(movement);
        _context.SaveJson();
    }
}

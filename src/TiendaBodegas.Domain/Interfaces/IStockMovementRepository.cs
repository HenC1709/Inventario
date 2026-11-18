using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.Domain.Interfaces;

/// <summary>
/// Repository contract for managing <see cref="StockMovement"/> persistence and queries.
/// </summary>
public interface IStockMovementRepository : IRepository<StockMovement>
{
    /// <summary>
    /// Retrieves all stock movements where the specified warehouse is either the source or destination.
    /// </summary>
    IEnumerable<StockMovement> GetByWarehouseId(int warehouseId);

    /// <summary>
    /// Retrieves all stock movements matching the specified movement type.
    /// </summary>
    IEnumerable<StockMovement> GetByMovementType(MovementType movementType);
}

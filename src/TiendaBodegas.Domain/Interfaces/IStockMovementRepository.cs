using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.Domain.Interfaces;

public interface IStockMovementRepository : IRepository<StockMovement>
{
    IEnumerable<StockMovement> GetByWarehouseId(int warehouseId);
    IEnumerable<StockMovement> GetByMovementType(MovementType movementType);
    new void Add(StockMovement movement);
}

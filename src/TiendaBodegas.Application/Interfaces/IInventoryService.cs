using TiendaBodegas.Domain.Entities;

namespace TiendaBodegas.Application.Interfaces
{
public interface IInventoryService
    {
        void RegisterEntry(int productId, int warehouseId, int quantity, string notes);
        void RegisterExit(int productId, int warehouseId, int quantity, string notes);
        void TransferStock(int productId, int sourceWarehouseId, int destinationWarehouseId, int quantity, string notes);

        IEnumerable<Inventory> GetStockByWarehouse(int warehouseId);
        int GetTotalStockByProduct(int productId);
    }

}
using TiendaBodegas.Domain.Entities;

namespace TiendaBodegas.Application.Interfaces
{
    /// <summary>
    /// Application service contract for recording inventory movements and querying stock levels.
    /// </summary>
    public interface IInventoryService
    {
        /// <summary>
        /// Registers a stock entry into the specified warehouse.
        /// </summary>
        void RegisterEntry(int productId, int warehouseId, int quantity, string notes);

        /// <summary>
        /// Registers a stock exit from the specified warehouse.
        /// </summary>
        void RegisterExit(int productId, int warehouseId, int quantity, string notes);

        /// <summary>
        /// Transfers stock of a product from a source warehouse to a destination warehouse.
        /// </summary>
        void TransferStock(int productId, int sourceWarehouseId, int destinationWarehouseId, int quantity, string notes);

        /// <summary>
        /// Calculates current stock per product in the specified warehouse.
        /// </summary>
        IEnumerable<Inventory> GetStockByWarehouse(int warehouseId);

        /// <summary>
        /// Calculates the total net stock of a product across all warehouses.
        /// </summary>
        int GetTotalStockByProduct(int productId);
    }

}
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Domain.Entities;

namespace TiendaBodegas.Application.Interfaces
{
    /// <summary>
    /// Application service contract for recording inventory movements and querying stock levels.
    /// </summary>
    public interface IInventoryService
    {
        /// <summary>
        /// Registers an entry, exit, or transfer movement from a unified request object.
        /// </summary>
        void RegisterMovement(StockMovementRequest request);

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
        /// Retrieves stock movements matching optional filter criteria, ordered from newest to oldest.
        /// </summary>
        IEnumerable<StockMovement> GetMovements(StockMovementFilter? filter = null);

        /// <summary>
        /// Retrieves enriched stock movements with readable product and warehouse labels matching optional filters.
        /// </summary>
        IEnumerable<StockMovementDisplayDto> GetMovementDetails(StockMovementFilter? filter = null);

        /// <summary>
        /// Calculates the current stock balance of a product in a specific warehouse.
        /// </summary>
        int GetProductStockInWarehouse(int productId, int warehouseId);

        /// <summary>
        /// Calculates current stock per product in the specified warehouse.
        /// </summary>
        IEnumerable<Inventory> GetStockByWarehouse(int warehouseId);

        /// <summary>
        /// Calculates the total net stock of a product across all warehouses.
        /// </summary>
        int GetTotalStockByProduct(int productId);

        /// <summary>
        /// Builds a stock summary report per warehouse and product, including low-stock flags.
        /// </summary>
        IEnumerable<WarehouseStockDto> GetWarehouseStockReport(int? warehouseId = null);

        /// <summary>
        /// Builds a global stock summary per product across all warehouses, including low-stock flags.
        /// </summary>
        IEnumerable<ProductStockSummaryDto> GetProductStockSummary();
    }

}
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Domain.Entities;

namespace TiendaBodegas.Application.Interfaces;

/// <summary>
/// Application service contract for recording single-warehouse inventory movements, stock queries, and audit reports.
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Registers an entry or exit movement from a unified request object.
    /// </summary>
    void RegisterMovement(StockMovementRequest request);

    /// <summary>
    /// Registers a stock entry (replenishment/purchase) for a valid product.
    /// </summary>
    void RegisterEntry(int productId, int quantity, string notes);

    /// <summary>
    /// Registers a stock exit (sale/adjustment) after verifying available stock.
    /// </summary>
    void RegisterExit(int productId, int quantity, string notes);

    /// <summary>
    /// Retrieves stock movements matching optional filter criteria, ordered from newest to oldest.
    /// </summary>
    IEnumerable<StockMovement> GetMovements(StockMovementFilter? filter = null);

    /// <summary>
    /// Retrieves enriched stock movements with product labels and valuation matching optional filters.
    /// </summary>
    IEnumerable<StockMovementDisplayDto> GetMovementDetails(StockMovementFilter? filter = null);

    /// <summary>
    /// Calculates the net global stock of a product (entries minus exits).
    /// </summary>
    int GetTotalStockByProduct(int productId);

    /// <summary>
    /// Builds a global stock summary per product including supplier info, pricing, and stock status labels.
    /// </summary>
    IEnumerable<ProductStockSummaryDto> GetProductStockSummary();

    /// <summary>
    /// Builds an inventory valuation summary grouped by category (purchase cost vs. sale value).
    /// </summary>
    IEnumerable<CategoryValuationDto> GetCategoryValuationReport();

    /// <summary>
    /// Finds products that have had no inventory movements within the specified trailing <paramref name="days"/> window.
    /// </summary>
    IEnumerable<LowRotationProductDto> GetLowRotationProducts(int days = 30, DateTime? referenceDate = null);
}

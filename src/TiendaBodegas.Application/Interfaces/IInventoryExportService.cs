using TiendaBodegas.Application.DTOs;

namespace TiendaBodegas.Application.Interfaces;

/// <summary>
/// Application service contract for exporting inventory, valuation, and audit reports to CSV format.
/// </summary>
public interface IInventoryExportService
{
    /// <summary>
    /// Generates an RFC-4180 compliant CSV string for the supplied inventory summary items.
    /// </summary>
    string GenerateInventoryCsv(IEnumerable<ProductStockSummaryDto> items);

    /// <summary>
    /// Generates an RFC-4180 compliant CSV string summarizing purchase cost, sale value, and margin by category.
    /// </summary>
    string GenerateValuationCsv(IEnumerable<CategoryValuationDto> categories);

    /// <summary>
    /// Generates an RFC-4180 compliant CSV string for products with low movement rotation.
    /// </summary>
    string GenerateLowRotationCsv(IEnumerable<LowRotationProductDto> items);
}

using TiendaBodegas.Application.DTOs;

namespace TiendaBodegas.Application.Interfaces;

/// <summary>
/// Application service contract for exporting inventory summary records to CSV format.
/// </summary>
public interface IInventoryExportService
{
    /// <summary>
    /// Generates an RFC-4180 compliant CSV string for the supplied inventory summary items.
    /// </summary>
    string GenerateInventoryCsv(IEnumerable<ProductStockSummaryDto> items);
}

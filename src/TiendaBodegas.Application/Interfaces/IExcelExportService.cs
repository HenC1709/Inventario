using TiendaBodegas.Application.DTOs;

namespace TiendaBodegas.Application.Interfaces;

/// <summary>
/// Application service contract for generating formatted Excel (.xlsx) workbooks from inventory, valuation, and audit projections.
/// </summary>
public interface IExcelExportService
{
    /// <summary>
    /// Generates an Excel workbook summarizing purchase cost, sale valuation, and estimated margin by category.
    /// </summary>
    /// <param name="categories">Category valuation rows to export.</param>
    /// <param name="businessName">Optional store/warehouse header name.</param>
    /// <param name="currencyCode">Active ISO currency code (<c>USD</c>, <c>COP</c>, <c>EUR</c>).</param>
    byte[] GenerateValuationExcel(
        IEnumerable<CategoryValuationDto> categories,
        string? businessName = null,
        string? currencyCode = null);

    /// <summary>
    /// Generates an Excel workbook containing the current product inventory stock, supplier, and pricing summary.
    /// </summary>
    /// <param name="items">Inventory summary rows to export.</param>
    /// <param name="businessName">Optional store/warehouse header name.</param>
    /// <param name="currencyCode">Active ISO currency code (<c>USD</c>, <c>COP</c>, <c>EUR</c>).</param>
    byte[] GenerateInventoryExcel(
        IEnumerable<ProductStockSummaryDto> items,
        string? businessName = null,
        string? currencyCode = null);

    /// <summary>
    /// Generates an Excel workbook listing low-rotation products without movements in the audit window.
    /// </summary>
    /// <param name="items">Low-rotation audit rows to export.</param>
    /// <param name="businessName">Optional store/warehouse header name.</param>
    /// <param name="auditDays">Lookback window in days represented by the audit.</param>
    byte[] GenerateLowRotationExcel(
        IEnumerable<LowRotationProductDto> items,
        string? businessName = null,
        int auditDays = 30);
}

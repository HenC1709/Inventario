using TiendaBodegas.Application.DTOs;

namespace TiendaBodegas.Application.Interfaces;

/// <summary>
/// Application service contract for generating formatted PDF reports from inventory, valuation, and audit projections.
/// </summary>
public interface IPdfExportService
{
    /// <summary>
    /// Generates a PDF document summarizing purchase cost, sale valuation, and estimated margin by category.
    /// </summary>
    /// <param name="categories">Category valuation rows to render.</param>
    /// <param name="businessName">Optional store/warehouse header name.</param>
    /// <param name="currencyCode">Active ISO currency code (<c>USD</c>, <c>COP</c>, <c>EUR</c>).</param>
    byte[] GenerateValuationPdf(
        IEnumerable<CategoryValuationDto> categories,
        string? businessName = null,
        string? currencyCode = null);

    /// <summary>
    /// Generates a PDF document containing the current product inventory stock and price summary.
    /// </summary>
    /// <param name="items">Inventory summary rows to render.</param>
    /// <param name="businessName">Optional store/warehouse header name.</param>
    /// <param name="currencyCode">Active ISO currency code (<c>USD</c>, <c>COP</c>, <c>EUR</c>).</param>
    byte[] GenerateInventoryPdf(
        IEnumerable<ProductStockSummaryDto> items,
        string? businessName = null,
        string? currencyCode = null);

    /// <summary>
    /// Generates a PDF document listing low-rotation products without movements in the audit window.
    /// </summary>
    /// <param name="items">Low-rotation audit rows to render.</param>
    /// <param name="businessName">Optional store/warehouse header name.</param>
    /// <param name="auditDays">Lookback window in days represented by the audit.</param>
    byte[] GenerateLowRotationPdf(
        IEnumerable<LowRotationProductDto> items,
        string? businessName = null,
        int auditDays = 30);
}

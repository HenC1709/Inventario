namespace TiendaBodegas.Application.DTOs;

/// <summary>
/// Projection summarizing purchase cost, sale valuation, and estimated margin grouped by product category.
/// </summary>
public sealed record CategoryValuationDto(
    string Category,
    int ProductCount,
    int TotalUnits,
    decimal TotalPurchaseCost,
    decimal TotalSaleValue,
    decimal EstimatedMargin);

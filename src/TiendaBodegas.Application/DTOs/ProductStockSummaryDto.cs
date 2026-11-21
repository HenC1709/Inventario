namespace TiendaBodegas.Application.DTOs;

/// <summary>
/// Projection summarizing a product's total net stock across all warehouses alongside its minimum threshold.
/// </summary>
public sealed record ProductStockSummaryDto(
    int ProductId,
    string ProductSku,
    string ProductName,
    int MinStock,
    int TotalStock,
    bool IsLowStock);

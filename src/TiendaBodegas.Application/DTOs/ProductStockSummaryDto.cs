namespace TiendaBodegas.Application.DTOs;

/// <summary>
/// Projection summarizing a product's global stock balance, supplier metadata, pricing, and semantic stock status.
/// </summary>
public sealed record ProductStockSummaryDto(
    int ProductId,
    string ProductSku,
    string ProductName,
    string Category,
    int? SupplierId,
    string SupplierName,
    decimal PurchasePrice,
    decimal SalePrice,
    int MinStock,
    int TotalStock,
    bool IsOutOfStock,
    bool IsLowStock,
    string StockStatusLabel);

namespace TiendaBodegas.Application.DTOs;

/// <summary>
/// Projection summarizing a product's current stock inside a specific warehouse alongside global stock and low-stock status.
/// </summary>
public sealed record WarehouseStockDto(
    int WarehouseId,
    string WarehouseName,
    int ProductId,
    string ProductSku,
    string ProductName,
    int MinStock,
    int Quantity,
    int TotalProductStock,
    bool IsLowStock);

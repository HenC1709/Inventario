using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.Application.DTOs;

/// <summary>
/// Enriched projection of a stock movement including product details and calculated monetary valuation.
/// </summary>
public sealed record StockMovementDisplayDto(
    int Id,
    DateTime Date,
    MovementType MovementType,
    string MovementTypeLabel,
    int ProductId,
    string ProductSku,
    string ProductName,
    string ProductDisplay,
    int Quantity,
    decimal UnitPrice,
    decimal TotalAmount,
    string Notes);

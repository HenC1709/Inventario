using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.Application.DTOs;

/// <summary>
/// Enriched projection of a stock movement including readable product and warehouse labels alongside IDs.
/// </summary>
public sealed record StockMovementDisplayDto(
    int Id,
    DateTime Date,
    MovementType MovementType,
    string MovementTypeLabel,
    int ProductId,
    string ProductName,
    string ProductDisplay,
    int Quantity,
    int? SourceWarehouseId,
    string SourceWarehouseName,
    string SourceWarehouseDisplay,
    int? DestinationWarehouseId,
    string DestinationWarehouseName,
    string DestinationWarehouseDisplay,
    string Notes);

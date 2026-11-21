using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.Application.DTOs;

/// <summary>
/// Data transfer object carrying the parameters required to register an entry, exit, or transfer movement.
/// </summary>
public sealed record StockMovementRequest(
    int ProductId,
    MovementType MovementType,
    int? SourceWarehouseId,
    int? DestinationWarehouseId,
    int Quantity,
    string Notes = "");

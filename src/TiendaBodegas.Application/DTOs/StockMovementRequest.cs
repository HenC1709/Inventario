using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.Application.DTOs;

/// <summary>
/// Data transfer object carrying the parameters required to register a single-warehouse entry or exit movement.
/// </summary>
public sealed record StockMovementRequest(
    int ProductId,
    MovementType MovementType,
    int Quantity,
    string Notes = "");

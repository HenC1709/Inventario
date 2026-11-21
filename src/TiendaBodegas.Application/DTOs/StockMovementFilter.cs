using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.Application.DTOs;

/// <summary>
/// Optional filter criteria for querying stock movement history.
/// </summary>
public sealed record StockMovementFilter(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    MovementType? MovementType = null,
    int? ProductId = null);

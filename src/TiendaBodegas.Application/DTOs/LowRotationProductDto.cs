namespace TiendaBodegas.Application.DTOs;

/// <summary>
/// Audit projection identifying a product with no inventory movements within the configured lookback window.
/// </summary>
public sealed record LowRotationProductDto(
    int ProductId,
    string ProductSku,
    string ProductName,
    string Category,
    int CurrentStock,
    DateTime? LastMovementDate,
    int? DaysWithoutMovement);

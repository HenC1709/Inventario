using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// ComboBox option representing either all movement types or a specific <see cref="MovementType"/> filter.
/// </summary>
public sealed record MovementTypeFilterOption(MovementType? Type, string Label);

/// <summary>
/// ComboBox option pairing an optional string filter value with its display label.
/// </summary>
public sealed record StringFilterOption(string? Value, string Label);

/// <summary>
/// ComboBox option representing either all suppliers or a specific supplier filter.
/// </summary>
public sealed record SupplierFilterOption(int? SupplierId, string Label);

/// <summary>
/// Pagination item representing a selectable page number and whether it is currently active.
/// </summary>
public sealed record PageNumberItem(int Number, bool IsCurrent);

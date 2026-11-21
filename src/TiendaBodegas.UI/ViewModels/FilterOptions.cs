using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// ComboBox option representing either all warehouses or a specific warehouse filter.
/// </summary>
public sealed record WarehouseFilterOption(int? WarehouseId, string Label);

/// <summary>
/// ComboBox option representing either all movement types or a specific <see cref="MovementType"/> filter.
/// </summary>
public sealed record MovementTypeFilterOption(MovementType? Type, string Label);

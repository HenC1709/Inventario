using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// ComboBox option representing either all movement types or a specific <see cref="MovementType"/> filter.
/// </summary>
public sealed record MovementTypeFilterOption(MovementType? Type, string Label);

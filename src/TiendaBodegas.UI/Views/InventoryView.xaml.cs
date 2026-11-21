using System.Windows.Controls;

namespace TiendaBodegas.UI.Views;

/// <summary>
/// Unified Inventory view presenting KPI cards, search/filter toolbar, sortable paginated grid, and row actions.
/// </summary>
public partial class InventoryView : UserControl
{
    /// <summary>
    /// Initializes a new instance of <see cref="InventoryView"/>.
    /// </summary>
    public InventoryView()
    {
        InitializeComponent();
    }
}

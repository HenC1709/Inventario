using System.Windows;
using System.Windows.Controls;
using TiendaBodegas.UI.ViewModels;

namespace TiendaBodegas.UI.Views;

/// <summary>
/// Unified Inventory view presenting KPI cards, search/filter toolbar, sortable paginated grid, and row actions.
/// </summary>
public partial class InventoryView : UserControl
{
    /// <summary>
    /// Initializes a new instance of <see cref="InventoryView"/> and hooks viewport resize events.
    /// </summary>
    public InventoryView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
        => SyncViewportPageSize();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        => SyncViewportPageSize();

    private void OnProductsDataGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.HeightChanged)
            SyncViewportPageSize();
    }

    private void SyncViewportPageSize()
    {
        if (DataContext is InventoryViewModel viewModel && ProductsDataGrid.ActualHeight > 0)
        {
            viewModel.UpdatePageSizeForViewportHeight(ProductsDataGrid.ActualHeight);
        }
    }
}

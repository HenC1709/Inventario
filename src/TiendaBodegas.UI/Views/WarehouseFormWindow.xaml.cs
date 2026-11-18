using System.Windows;
using TiendaBodegas.UI.ViewModels;

namespace TiendaBodegas.UI.Views;

/// <summary>
/// Modal dialog window for creating or editing a warehouse.
/// </summary>
public partial class WarehouseFormWindow : Window
{
    /// <summary>
    /// Initializes a new instance of <see cref="WarehouseFormWindow"/> bound to the supplied ViewModel.
    /// </summary>
    public WarehouseFormWindow(WarehouseFormViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += result =>
        {
            DialogResult = result;
            Close();
        };
    }
}

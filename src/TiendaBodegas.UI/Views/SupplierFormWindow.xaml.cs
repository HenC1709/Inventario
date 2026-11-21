using System.Windows;
using TiendaBodegas.UI.ViewModels;

namespace TiendaBodegas.UI.Views;

/// <summary>
/// Modal dialog window for creating or editing a supplier.
/// </summary>
public partial class SupplierFormWindow : Window
{
    /// <summary>
    /// Initializes a new instance of <see cref="SupplierFormWindow"/> bound to the supplied ViewModel.
    /// </summary>
    public SupplierFormWindow(SupplierFormViewModel viewModel)
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

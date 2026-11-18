using System.Windows;
using TiendaBodegas.UI.ViewModels;

namespace TiendaBodegas.UI.Views;

/// <summary>
/// Modal dialog window for creating or editing a product.
/// </summary>
public partial class ProductFormWindow : Window
{
    /// <summary>
    /// Initializes a new instance of <see cref="ProductFormWindow"/> bound to the supplied ViewModel.
    /// </summary>
    public ProductFormWindow(ProductFormViewModel viewModel)
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

using System.Windows;
using TiendaBodegas.UI.ViewModels;

namespace TiendaBodegas.UI.Views;

/// <summary>
/// Modal dialog window for registering inventory movements.
/// </summary>
public partial class MovementFormWindow : Window
{
    /// <summary>
    /// Initializes a new instance of <see cref="MovementFormWindow"/> bound to the supplied ViewModel.
    /// </summary>
    public MovementFormWindow(MovementFormViewModel viewModel)
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

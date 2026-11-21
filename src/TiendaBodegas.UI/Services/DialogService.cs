using System.Windows;
using TiendaBodegas.UI.ViewModels;
using TiendaBodegas.UI.Views;

namespace TiendaBodegas.UI.Services;

/// <summary>
/// WPF implementation of <see cref="IDialogService"/> using modal windows and message boxes.
/// </summary>
public class DialogService : IDialogService
{
    /// <summary>
    /// Opens <see cref="ProductFormWindow"/> modally over the main window.
    /// </summary>
    public bool ShowProductForm(ProductFormViewModel viewModel)
    {
        var window = new ProductFormWindow(viewModel)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        return window.ShowDialog() == true;
    }

    /// <summary>
    /// Opens <see cref="SupplierFormWindow"/> modally over the main window.
    /// </summary>
    public bool ShowSupplierForm(SupplierFormViewModel viewModel)
    {
        var window = new SupplierFormWindow(viewModel)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        return window.ShowDialog() == true;
    }

    /// <summary>
    /// Opens <see cref="MovementFormWindow"/> modally over the main window.
    /// </summary>
    public bool ShowMovementForm(MovementFormViewModel viewModel)
    {
        var window = new MovementFormWindow(viewModel)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        return window.ShowDialog() == true;
    }

    /// <summary>
    /// Displays a Yes/No confirmation message box and returns true when the user selects Yes.
    /// </summary>
    public bool Confirm(string message, string title)
    {
        var result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }
}

using Microsoft.Win32;
using TiendaBodegas.UI.ViewModels;
using TiendaBodegas.UI.Views;

namespace TiendaBodegas.UI.Services;

/// <summary>
/// WPF implementation of <see cref="IDialogService"/> using styled modal windows and native file dialogs.
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
    /// Displays the styled <see cref="ConfirmDialogWindow"/> and returns true when the user confirms.
    /// </summary>
    public bool Confirm(string message, string title)
    {
        var window = new ConfirmDialogWindow(title, message)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        return window.ShowDialog() == true;
    }

    /// <summary>
    /// Displays a <see cref="SaveFileDialog"/> and returns the chosen path, or null when cancelled.
    /// </summary>
    public string? ShowSaveFileDialog(string defaultFileName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            FileName = defaultFileName,
            Filter = filter,
            AddExtension = true
        };

        return dialog.ShowDialog(System.Windows.Application.Current?.MainWindow) == true
            ? dialog.FileName
            : null;
    }

    /// <summary>
    /// Displays an <see cref="OpenFileDialog"/> and returns the chosen existing file path, or null when cancelled.
    /// </summary>
    public string? ShowOpenFileDialog(string filter)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog(System.Windows.Application.Current?.MainWindow) == true
            ? dialog.FileName
            : null;
    }
}

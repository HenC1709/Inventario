using TiendaBodegas.UI.ViewModels;

namespace TiendaBodegas.UI.Services;

/// <summary>
/// Abstraction for displaying modal forms, confirmation dialogs, and file save prompts from ViewModels.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Opens the modal product form and returns whether the user saved changes.
    /// </summary>
    bool ShowProductForm(ProductFormViewModel viewModel);

    /// <summary>
    /// Opens the modal supplier form and returns whether the user saved changes.
    /// </summary>
    bool ShowSupplierForm(SupplierFormViewModel viewModel);

    /// <summary>
    /// Opens the modal movement registration form and returns whether the user registered a movement.
    /// </summary>
    bool ShowMovementForm(MovementFormViewModel viewModel);

    /// <summary>
    /// Displays a styled confirmation dialog and returns whether the user accepted.
    /// </summary>
    bool Confirm(string message, string title);

    /// <summary>
    /// Opens a save-file dialog and returns the selected file path, or null if cancelled.
    /// </summary>
    string? ShowSaveFileDialog(string defaultFileName, string filter);

    /// <summary>
    /// Opens an open-file dialog and returns the selected existing file path, or null if cancelled.
    /// </summary>
    string? ShowOpenFileDialog(string filter) => null;
}

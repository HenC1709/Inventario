using TiendaBodegas.UI.ViewModels;

namespace TiendaBodegas.UI.Services;

/// <summary>
/// Abstraction for displaying modal forms and confirmation dialogs from ViewModels.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Opens the modal product form and returns whether the user saved changes.
    /// </summary>
    bool ShowProductForm(ProductFormViewModel viewModel);

    /// <summary>
    /// Opens the modal warehouse form and returns whether the user saved changes.
    /// </summary>
    bool ShowWarehouseForm(WarehouseFormViewModel viewModel);

    /// <summary>
    /// Displays a confirmation prompt and returns whether the user accepted.
    /// </summary>
    bool Confirm(string message, string title);
}

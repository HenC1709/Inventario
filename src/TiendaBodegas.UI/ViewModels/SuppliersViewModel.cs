using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.UI.Services;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// ViewModel managing the searchable supplier directory and CRUD actions.
/// </summary>
public partial class SuppliersViewModel : ObservableObject
{
    private readonly ICatalogService _catalogService;
    private readonly IDialogService _dialogService;
    private List<Supplier> _allSuppliers = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditSupplierCommand), nameof(DeleteSupplierCommand))]
    private Supplier? _selectedSupplier;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isErrorMessage;

    /// <summary>
    /// Gets the filtered observable collection of suppliers bound to the UI.
    /// </summary>
    public ObservableCollection<Supplier> Suppliers { get; } = new();

    /// <summary>
    /// Initializes a new instance of <see cref="SuppliersViewModel"/> and loads initial data.
    /// </summary>
    public SuppliersViewModel(ICatalogService catalogService, IDialogService dialogService)
    {
        _catalogService = catalogService;
        _dialogService = dialogService;
        LoadData();
    }

    /// <summary>
    /// Reloads all suppliers from the catalog service and applies the current search filter.
    /// </summary>
    public void LoadData()
    {
        _allSuppliers = _catalogService.GetAllSuppliers().ToList();
        ApplyFilter();
    }

    /// <summary>
    /// Opens the modal form to create a new supplier.
    /// </summary>
    [RelayCommand]
    public void CreateSupplier()
    {
        ClearStatus();
        var formViewModel = new SupplierFormViewModel(_catalogService);
        if (_dialogService.ShowSupplierForm(formViewModel))
        {
            LoadData();
            SetSuccessStatus("Proveedor creado correctamente.");
        }
    }

    /// <summary>
    /// Opens the modal form to edit the currently selected supplier.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedSupplier))]
    public void EditSupplier()
    {
        if (SelectedSupplier == null)
            return;

        ClearStatus();
        var formViewModel = new SupplierFormViewModel(_catalogService, SelectedSupplier);
        if (_dialogService.ShowSupplierForm(formViewModel))
        {
            LoadData();
            SetSuccessStatus("Proveedor actualizado correctamente.");
        }
    }

    /// <summary>
    /// Requests confirmation and deletes the selected supplier when permitted by business rules.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedSupplier))]
    public void DeleteSupplier()
    {
        if (SelectedSupplier == null)
            return;

        ClearStatus();
        var supplier = SelectedSupplier;
        if (!_dialogService.Confirm($"¿Desea eliminar el proveedor '{supplier.Name}'?", "Confirmar eliminación"))
            return;

        try
        {
            _catalogService.DeleteSupplier(supplier.Id);
            SelectedSupplier = null;
            LoadData();
            SetSuccessStatus("Proveedor eliminado correctamente.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            SetErrorStatus(ex.Message);
        }
    }

    /// <summary>
    /// Updates the filtered list whenever the search text changes.
    /// </summary>
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    /// <summary>
    /// Determines whether edit and delete actions are enabled.
    /// </summary>
    private bool HasSelectedSupplier() => SelectedSupplier != null;

    /// <summary>
    /// Filters the cached supplier list by code, business name, contact, or main category (case-insensitive).
    /// </summary>
    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        var filtered = string.IsNullOrEmpty(term)
            ? _allSuppliers
            : _allSuppliers.Where(supplier =>
                supplier.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                supplier.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                supplier.Contact.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                supplier.MainCategory.Contains(term, StringComparison.OrdinalIgnoreCase));

        Suppliers.Clear();
        foreach (var supplier in filtered)
        {
            Suppliers.Add(supplier);
        }
    }

    /// <summary>
    /// Clears the visible status banner.
    /// </summary>
    private void ClearStatus()
    {
        StatusMessage = string.Empty;
        IsErrorMessage = false;
    }

    /// <summary>
    /// Sets a success status banner message.
    /// </summary>
    private void SetSuccessStatus(string message)
    {
        IsErrorMessage = false;
        StatusMessage = message;
    }

    /// <summary>
    /// Sets an error status banner message.
    /// </summary>
    private void SetErrorStatus(string message)
    {
        IsErrorMessage = true;
        StatusMessage = message;
    }
}

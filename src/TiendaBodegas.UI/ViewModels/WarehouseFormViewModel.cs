using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Entities;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// Modal form ViewModel for creating or editing a <see cref="Warehouse"/> with <see cref="System.ComponentModel.INotifyDataErrorInfo"/> validation.
/// </summary>
public partial class WarehouseFormViewModel : ValidatableViewModel
{
    private readonly ICatalogService _catalogService;
    private readonly int? _editingWarehouseId;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _nameError = string.Empty;

    [ObservableProperty]
    private string _addressError = string.Empty;

    [ObservableProperty]
    private string _formError = string.Empty;

    /// <summary>
    /// Gets the dialog window title reflecting create or edit mode.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Raised when the form requests closing with the dialog outcome (true = saved, false = cancelled).
    /// </summary>
    public event Action<bool>? RequestClose;

    /// <summary>
    /// Initializes a new instance of <see cref="WarehouseFormViewModel"/> in create or edit mode.
    /// </summary>
    public WarehouseFormViewModel(ICatalogService catalogService, Warehouse? warehouseToEdit = null)
    {
        _catalogService = catalogService;

        if (warehouseToEdit != null)
        {
            _editingWarehouseId = warehouseToEdit.Id;
            Title = "Editar Bodega";
            _name = warehouseToEdit.Name;
            _address = warehouseToEdit.Address;
        }
        else
        {
            Title = "Nueva Bodega";
        }
    }

    /// <summary>
    /// Revalidates the warehouse name whenever it changes.
    /// </summary>
    partial void OnNameChanged(string value) => ValidateName();

    /// <summary>
    /// Revalidates the warehouse address whenever it changes.
    /// </summary>
    partial void OnAddressChanged(string value) => ValidateAddress();

    /// <summary>
    /// Validates all fields and persists the warehouse when valid, keeping the dialog open on errors.
    /// </summary>
    [RelayCommand]
    public void Save()
    {
        FormError = string.Empty;
        ValidateAll();

        if (HasErrors)
            return;

        try
        {
            if (_editingWarehouseId.HasValue)
            {
                _catalogService.UpdateWarehouse(_editingWarehouseId.Value, Name, Address);
            }
            else
            {
                _catalogService.CreateWarehouse(Name, Address);
            }

            RequestClose?.Invoke(true);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            FormError = ex.Message;
        }
    }

    /// <summary>
    /// Cancels editing and closes the dialog with a false result.
    /// </summary>
    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    /// <summary>
    /// Runs validation across all warehouse form fields.
    /// </summary>
    private void ValidateAll()
    {
        ValidateName();
        ValidateAddress();
    }

    /// <summary>
    /// Validates that the warehouse name is non-empty.
    /// </summary>
    private void ValidateName()
    {
        var error = string.IsNullOrWhiteSpace(Name)
            ? "El nombre es obligatorio."
            : string.Empty;

        NameError = error;
        SetPropertyError(nameof(Name), error);
    }

    /// <summary>
    /// Validates that the warehouse address is non-empty.
    /// </summary>
    private void ValidateAddress()
    {
        var error = string.IsNullOrWhiteSpace(Address)
            ? "La dirección es obligatoria."
            : string.Empty;

        AddressError = error;
        SetPropertyError(nameof(Address), error);
    }
}

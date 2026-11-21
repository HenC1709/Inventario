using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// Display option pairing a <see cref="SupplierStatus"/> value with its localized Spanish label.
/// </summary>
public sealed record SupplierStatusOption(SupplierStatus Status, string Label);

/// <summary>
/// Modal form ViewModel for creating or editing a <see cref="Supplier"/> with <see cref="System.ComponentModel.INotifyDataErrorInfo"/> validation.
/// </summary>
public partial class SupplierFormViewModel : ValidatableViewModel
{
    private readonly ICatalogService _catalogService;
    private readonly int? _editingSupplierId;

    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _contact = string.Empty;

    [ObservableProperty]
    private string _mainCategory = string.Empty;

    [ObservableProperty]
    private SupplierStatus _selectedStatus = SupplierStatus.Active;

    [ObservableProperty]
    private string _codeError = string.Empty;

    [ObservableProperty]
    private string _nameError = string.Empty;

    [ObservableProperty]
    private string _contactError = string.Empty;

    [ObservableProperty]
    private string _mainCategoryError = string.Empty;

    [ObservableProperty]
    private string _formError = string.Empty;

    /// <summary>
    /// Gets the selectable supplier status options displayed in the modal form.
    /// </summary>
    public IReadOnlyList<SupplierStatusOption> StatusOptions { get; } = new[]
    {
        new SupplierStatusOption(SupplierStatus.Active, "Activo"),
        new SupplierStatusOption(SupplierStatus.UnderReview, "En revisión")
    };

    /// <summary>
    /// Gets the dialog window title reflecting create or edit mode.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Raised when the form requests closing with the dialog outcome (true = saved, false = cancelled).
    /// </summary>
    public event Action<bool>? RequestClose;

    /// <summary>
    /// Initializes a new instance of <see cref="SupplierFormViewModel"/> in create or edit mode.
    /// </summary>
    public SupplierFormViewModel(ICatalogService catalogService, Supplier? supplierToEdit = null)
    {
        _catalogService = catalogService;

        if (supplierToEdit != null)
        {
            _editingSupplierId = supplierToEdit.Id;
            Title = "Editar Proveedor";
            _code = supplierToEdit.Code;
            _name = supplierToEdit.Name;
            _contact = supplierToEdit.Contact;
            _mainCategory = supplierToEdit.MainCategory;
            _selectedStatus = supplierToEdit.Status;
        }
        else
        {
            Title = "Nuevo Proveedor";
        }
    }

    /// <summary>
    /// Revalidates the optional supplier code whenever it changes.
    /// </summary>
    partial void OnCodeChanged(string value) => ValidateCode();

    /// <summary>
    /// Revalidates the supplier business name whenever it changes.
    /// </summary>
    partial void OnNameChanged(string value) => ValidateName();

    /// <summary>
    /// Revalidates the supplier contact whenever it changes.
    /// </summary>
    partial void OnContactChanged(string value) => ValidateContact();

    /// <summary>
    /// Revalidates the supplier main category whenever it changes.
    /// </summary>
    partial void OnMainCategoryChanged(string value) => ValidateMainCategory();

    /// <summary>
    /// Validates all fields and persists the supplier when valid, keeping the dialog open on errors.
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
            if (_editingSupplierId.HasValue)
            {
                _catalogService.UpdateSupplier(
                    _editingSupplierId.Value,
                    Name,
                    Contact,
                    MainCategory,
                    SelectedStatus,
                    Code);
            }
            else
            {
                _catalogService.CreateSupplier(
                    Name,
                    Contact,
                    MainCategory,
                    SelectedStatus,
                    Code);
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
    /// Runs validation across all supplier form fields.
    /// </summary>
    private void ValidateAll()
    {
        ValidateCode();
        ValidateName();
        ValidateContact();
        ValidateMainCategory();
    }

    /// <summary>
    /// Validates that any entered supplier code is unique across suppliers.
    /// </summary>
    private void ValidateCode()
    {
        var error = !string.IsNullOrWhiteSpace(Code) && !_catalogService.IsSupplierCodeAvailable(Code, _editingSupplierId)
            ? "Ya existe un proveedor con este código."
            : string.Empty;

        CodeError = error;
        SetPropertyError(nameof(Code), error);
    }

    /// <summary>
    /// Validates that the supplier business name is non-empty.
    /// </summary>
    private void ValidateName()
    {
        var error = string.IsNullOrWhiteSpace(Name)
            ? "La razón social es obligatoria."
            : string.Empty;

        NameError = error;
        SetPropertyError(nameof(Name), error);
    }

    /// <summary>
    /// Validates that the supplier contact is non-empty.
    /// </summary>
    private void ValidateContact()
    {
        var error = string.IsNullOrWhiteSpace(Contact)
            ? "El contacto es obligatorio."
            : string.Empty;

        ContactError = error;
        SetPropertyError(nameof(Contact), error);
    }

    /// <summary>
    /// Validates that the supplier main category is non-empty.
    /// </summary>
    private void ValidateMainCategory()
    {
        var error = string.IsNullOrWhiteSpace(MainCategory)
            ? "La categoría principal es obligatoria."
            : string.Empty;

        MainCategoryError = error;
        SetPropertyError(nameof(MainCategory), error);
    }
}

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Entities;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// Modal form ViewModel for creating or editing a <see cref="Product"/> with <see cref="System.ComponentModel.INotifyDataErrorInfo"/> validation.
/// </summary>
public partial class ProductFormViewModel : ValidatableViewModel
{
    private readonly ICatalogService _catalogService;
    private readonly int? _editingProductId;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _sku = string.Empty;

    [ObservableProperty]
    private string _priceText = string.Empty;

    [ObservableProperty]
    private string _minStockText = "0";

    [ObservableProperty]
    private string _nameError = string.Empty;

    [ObservableProperty]
    private string _skuError = string.Empty;

    [ObservableProperty]
    private string _priceError = string.Empty;

    [ObservableProperty]
    private string _minStockError = string.Empty;

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
    /// Initializes a new instance of <see cref="ProductFormViewModel"/> in create or edit mode.
    /// </summary>
    public ProductFormViewModel(ICatalogService catalogService, Product? productToEdit = null)
    {
        _catalogService = catalogService;

        if (productToEdit != null)
        {
            _editingProductId = productToEdit.Id;
            Title = "Editar Producto";
            _name = productToEdit.Name;
            _sku = productToEdit.SKU;
            _priceText = productToEdit.Price.ToString("0.##", CultureInfo.InvariantCulture);
            _minStockText = productToEdit.MinStock.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            Title = "Nuevo Producto";
        }
    }

    /// <summary>
    /// Revalidates the product name whenever it changes.
    /// </summary>
    partial void OnNameChanged(string value) => ValidateName();

    /// <summary>
    /// Revalidates the SKU whenever it changes.
    /// </summary>
    partial void OnSkuChanged(string value) => ValidateSku();

    /// <summary>
    /// Revalidates the price text whenever it changes.
    /// </summary>
    partial void OnPriceTextChanged(string value) => ValidatePrice();

    /// <summary>
    /// Revalidates the minimum stock text whenever it changes.
    /// </summary>
    partial void OnMinStockTextChanged(string value) => ValidateMinStock();

    /// <summary>
    /// Validates all fields and persists the product when valid, keeping the dialog open on validation or domain errors.
    /// </summary>
    [RelayCommand]
    public void Save()
    {
        FormError = string.Empty;
        ValidateAll();

        if (HasErrors
            || !TryParsePositivePrice(PriceText, out var price)
            || !TryParseNonNegativeInt(MinStockText, out var minStock))
            return;

        try
        {
            if (_editingProductId.HasValue)
            {
                _catalogService.UpdateProduct(_editingProductId.Value, Name, Sku, price, minStock);
            }
            else
            {
                _catalogService.CreateProduct(Name, Sku, price, minStock);
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
    /// Runs validation across all product form fields.
    /// </summary>
    private void ValidateAll()
    {
        ValidateName();
        ValidateSku();
        ValidatePrice();
        ValidateMinStock();
    }

    /// <summary>
    /// Validates that the product name is non-empty.
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
    /// Validates that the SKU is non-empty and not already assigned to another product.
    /// </summary>
    private void ValidateSku()
    {
        string error;
        if (string.IsNullOrWhiteSpace(Sku))
        {
            error = "El SKU es obligatorio.";
        }
        else if (!_catalogService.IsSkuAvailable(Sku, _editingProductId))
        {
            error = "Ya existe un producto con este SKU.";
        }
        else
        {
            error = string.Empty;
        }

        SkuError = error;
        SetPropertyError(nameof(Sku), error);
    }

    /// <summary>
    /// Validates that the price parses as a positive decimal number.
    /// </summary>
    private void ValidatePrice()
    {
        var error = TryParsePositivePrice(PriceText, out _)
            ? string.Empty
            : "Ingrese un precio válido mayor a 0.";

        PriceError = error;
        SetPropertyError(nameof(PriceText), error);
    }

    /// <summary>
    /// Validates that the minimum stock parses as a non-negative integer.
    /// </summary>
    private void ValidateMinStock()
    {
        var error = TryParseNonNegativeInt(MinStockText, out _)
            ? string.Empty
            : "Ingrese un stock mínimo entero mayor o igual a 0.";

        MinStockError = error;
        SetPropertyError(nameof(MinStockText), error);
    }

    /// <summary>
    /// Parses a decimal price supporting dot or comma decimal separators and checks that it is strictly positive.
    /// </summary>
    private static bool TryParsePositivePrice(string? text, out decimal price)
    {
        price = 0m;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        var hasComma = trimmed.Contains(',');
        var hasDot = trimmed.Contains('.');

        if (hasComma && !hasDot)
            trimmed = trimmed.Replace(',', '.');

        return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out price)
            && price > 0m;
    }

    /// <summary>
    /// Parses a non-negative integer value from form input text.
    /// </summary>
    private static bool TryParseNonNegativeInt(string? text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value >= 0;
    }
}

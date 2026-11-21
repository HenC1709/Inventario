using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Entities;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// Modal form ViewModel for creating or editing a <see cref="Product"/> and synchronizing its stock level via <see cref="IInventoryService"/>.
/// </summary>
public partial class ProductFormViewModel : ValidatableViewModel
{
    private readonly ICatalogService _catalogService;
    private readonly IInventoryService? _inventoryService;
    private readonly int? _editingProductId;
    private readonly int _initialStock;
    private readonly string _currencySymbol;
    private readonly string _currencyCode;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _sku = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private Supplier? _selectedSupplier;

    [ObservableProperty]
    private string _purchasePriceText = "0";

    [ObservableProperty]
    private string _salePriceText = string.Empty;

    [ObservableProperty]
    private string _stockText = "0";

    [ObservableProperty]
    private string _minStockText = "10";

    [ObservableProperty]
    private string _nameError = string.Empty;

    [ObservableProperty]
    private string _skuError = string.Empty;

    [ObservableProperty]
    private string _categoryError = string.Empty;

    [ObservableProperty]
    private string _purchasePriceError = string.Empty;

    [ObservableProperty]
    private string _salePriceError = string.Empty;

    [ObservableProperty]
    private string _stockError = string.Empty;

    [ObservableProperty]
    private string _minStockError = string.Empty;

    [ObservableProperty]
    private string _formError = string.Empty;

    /// <summary>
    /// Gets the list of available suppliers for selection.
    /// </summary>
    public ObservableCollection<Supplier> Suppliers { get; } = new();

    /// <summary>
    /// Gets the dialog window title reflecting create or edit mode.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the purchase price field label including the active currency code and symbol.
    /// </summary>
    public string PurchasePriceLabel => $"PRECIO COMPRA ({_currencyCode} {_currencySymbol})";

    /// <summary>
    /// Gets the sale price field label including the active currency code and symbol.
    /// </summary>
    public string SalePriceLabel => $"PRECIO VENTA ({_currencyCode} {_currencySymbol})";

    /// <summary>
    /// Gets a currency-appropriate numeric placeholder for price text boxes.
    /// </summary>
    public string PricePlaceholder => string.Equals(_currencyCode, "COP", StringComparison.OrdinalIgnoreCase)
        ? "Ej. 18000"
        : "0.00";

    /// <summary>
    /// Raised when the form requests closing with the dialog outcome (true = saved, false = cancelled).
    /// </summary>
    public event Action<bool>? RequestClose;

    /// <summary>
    /// Initializes a new instance of <see cref="ProductFormViewModel"/> in create or edit mode.
    /// </summary>
    public ProductFormViewModel(
        ICatalogService catalogService,
        IInventoryService? inventoryService = null,
        Product? productToEdit = null,
        int defaultMinStock = 10,
        string currencySymbol = "$",
        string currencyCode = "USD")
    {
        _catalogService = catalogService;
        _inventoryService = inventoryService;
        _currencyCode = string.IsNullOrWhiteSpace(currencyCode) ? "USD" : currencyCode.Trim().ToUpperInvariant();
        _currencySymbol = string.IsNullOrWhiteSpace(currencySymbol)
            ? (string.Equals(_currencyCode, "EUR", StringComparison.OrdinalIgnoreCase) ? "€" : "$")
            : currencySymbol;

        foreach (var supplier in catalogService.GetAllSuppliers())
        {
            Suppliers.Add(supplier);
        }

        if (productToEdit != null)
        {
            _editingProductId = productToEdit.Id;
            Title = "Editar Producto";
            _name = productToEdit.Name;
            _sku = productToEdit.SKU;
            _category = productToEdit.Category;
            _selectedSupplier = productToEdit.SupplierId.HasValue
                ? Suppliers.FirstOrDefault(s => s.Id == productToEdit.SupplierId.Value)
                : null;
            _purchasePriceText = productToEdit.PurchasePrice.ToString("0.##", CultureInfo.InvariantCulture);
            _salePriceText = productToEdit.SalePrice.ToString("0.##", CultureInfo.InvariantCulture);
            _initialStock = _inventoryService?.GetTotalStockByProduct(productToEdit.Id) ?? 0;
            _stockText = _initialStock.ToString(CultureInfo.InvariantCulture);
            _minStockText = productToEdit.MinStock.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            Title = "Agregar Nuevo Producto";
            _selectedSupplier = Suppliers.FirstOrDefault();
            _minStockText = Math.Max(0, defaultMinStock).ToString(CultureInfo.InvariantCulture);
        }
    }

    partial void OnNameChanged(string value) => ValidateName();
    partial void OnSkuChanged(string value) => ValidateSku();
    partial void OnCategoryChanged(string value) => ValidateCategory();
    partial void OnPurchasePriceTextChanged(string value) => ValidatePurchasePrice();
    partial void OnSalePriceTextChanged(string value) => ValidateSalePrice();
    partial void OnStockTextChanged(string value) => ValidateStock();
    partial void OnMinStockTextChanged(string value) => ValidateMinStock();

    /// <summary>
    /// Validates all fields, persists product metadata, and records initial or adjustment stock movements when applicable.
    /// </summary>
    [RelayCommand]
    public void Save()
    {
        FormError = string.Empty;
        ValidateAll();

        if (HasErrors
            || !TryParseNonNegativeDecimal(PurchasePriceText, _currencyCode, out var purchasePrice)
            || !TryParsePositiveDecimal(SalePriceText, _currencyCode, out var salePrice)
            || !TryParseNonNegativeInt(StockText, out var targetStock)
            || !TryParseNonNegativeInt(MinStockText, out var minStock))
        {
            return;
        }

        try
        {
            if (_editingProductId.HasValue)
            {
                _catalogService.UpdateProduct(
                    _editingProductId.Value,
                    Name,
                    Sku,
                    Category,
                    SelectedSupplier?.Id,
                    purchasePrice,
                    salePrice,
                    minStock);

                if (_inventoryService != null)
                {
                    var delta = targetStock - _initialStock;
                    if (delta > 0)
                        _inventoryService.RegisterEntry(_editingProductId.Value, delta, "Ajuste manual desde inventario");
                    else if (delta < 0)
                        _inventoryService.RegisterExit(_editingProductId.Value, -delta, "Ajuste manual desde inventario");
                }
            }
            else
            {
                var created = _catalogService.CreateProduct(
                    Name,
                    Sku,
                    Category,
                    SelectedSupplier?.Id,
                    purchasePrice,
                    salePrice,
                    minStock);

                if (_inventoryService != null && targetStock > 0)
                    _inventoryService.RegisterEntry(created.Id, targetStock, "Stock inicial");
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

    private void ValidateAll()
    {
        ValidateName();
        ValidateSku();
        ValidateCategory();
        ValidatePurchasePrice();
        ValidateSalePrice();
        ValidateStock();
        ValidateMinStock();
    }

    private void ValidateName()
    {
        var error = string.IsNullOrWhiteSpace(Name)
            ? "El nombre es obligatorio."
            : string.Empty;

        NameError = error;
        SetPropertyError(nameof(Name), error);
    }

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

    private void ValidateCategory()
    {
        var error = string.IsNullOrWhiteSpace(Category)
            ? "La categoría es obligatoria."
            : string.Empty;

        CategoryError = error;
        SetPropertyError(nameof(Category), error);
    }

    private void ValidatePurchasePrice()
    {
        var error = TryParseNonNegativeDecimal(PurchasePriceText, _currencyCode, out _)
            ? string.Empty
            : "Ingrese un precio de compra válido mayor o igual a 0.";

        PurchasePriceError = error;
        SetPropertyError(nameof(PurchasePriceText), error);
    }

    private void ValidateSalePrice()
    {
        var error = TryParsePositiveDecimal(SalePriceText, _currencyCode, out _)
            ? string.Empty
            : "Ingrese un precio de venta válido mayor a 0.";

        SalePriceError = error;
        SetPropertyError(nameof(SalePriceText), error);
    }

    private void ValidateStock()
    {
        var error = TryParseNonNegativeInt(StockText, out _)
            ? string.Empty
            : "Ingrese un stock entero mayor o igual a 0.";

        StockError = error;
        SetPropertyError(nameof(StockText), error);
    }

    private void ValidateMinStock()
    {
        var error = TryParseNonNegativeInt(MinStockText, out _)
            ? string.Empty
            : "Ingrese un stock mínimo entero mayor o igual a 0.";

        MinStockError = error;
        SetPropertyError(nameof(MinStockText), error);
    }

    private static bool TryParseNonNegativeDecimal(string? text, string currencyCode, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim()
            .Replace("COP", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("USD", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("EUR", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace("€", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        if (string.IsNullOrEmpty(trimmed))
            return false;

        var dotCount = trimmed.Count(c => c == '.');
        var commaCount = trimmed.Count(c => c == ',');

        if (dotCount > 0 && commaCount > 0)
        {
            if (trimmed.LastIndexOf(',') > trimmed.LastIndexOf('.'))
                trimmed = trimmed.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
            else
                trimmed = trimmed.Replace(",", string.Empty, StringComparison.Ordinal);
        }
        else if (commaCount > 1)
        {
            trimmed = trimmed.Replace(",", string.Empty, StringComparison.Ordinal);
        }
        else if (dotCount > 1)
        {
            trimmed = trimmed.Replace(".", string.Empty, StringComparison.Ordinal);
        }
        else if (commaCount == 1)
        {
            var parts = trimmed.Split(',');
            trimmed = IsThousandsGroup(parts[0], parts[1])
                ? parts[0] + parts[1]
                : parts[0] + "." + parts[1];
        }
        else if (dotCount == 1 && string.Equals(currencyCode, "COP", StringComparison.OrdinalIgnoreCase))
        {
            var parts = trimmed.Split('.');
            if (IsThousandsGroup(parts[0], parts[1]))
                trimmed = parts[0] + parts[1];
        }

        return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
            && value >= 0m;
    }

    private static bool IsThousandsGroup(string integerPart, string trailingPart)
    {
        return trailingPart.Length == 3
            && trailingPart.All(char.IsDigit)
            && integerPart.Length is >= 1 and <= 3
            && integerPart.All(char.IsDigit)
            && !string.Equals(integerPart, "0", StringComparison.Ordinal);
    }

    private static bool TryParsePositiveDecimal(string? text, string currencyCode, out decimal value)
    {
        return TryParseNonNegativeDecimal(text, currencyCode, out value) && value > 0m;
    }

    private static bool TryParseNonNegativeInt(string? text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value >= 0;
    }
}

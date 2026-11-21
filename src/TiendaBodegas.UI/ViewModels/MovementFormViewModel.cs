using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// Display option pairing a <see cref="MovementType"/> value with its localized Spanish label.
/// </summary>
public sealed record MovementTypeOption(MovementType Type, string Label);

/// <summary>
/// Modal form ViewModel for registering single-warehouse entries and exits with <see cref="System.ComponentModel.INotifyDataErrorInfo"/> validation.
/// </summary>
public partial class MovementFormViewModel : ValidatableViewModel
{
    private readonly IInventoryService _inventoryService;

    [ObservableProperty]
    private MovementType _selectedMovementType;

    [ObservableProperty]
    private Product? _selectedProduct;

    [ObservableProperty]
    private string _quantityText = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _productError = string.Empty;

    [ObservableProperty]
    private string _quantityError = string.Empty;

    [ObservableProperty]
    private string _formError = string.Empty;

    /// <summary>
    /// Gets the selectable movement types shown in the modal form.
    /// </summary>
    public IReadOnlyList<MovementTypeOption> MovementTypes { get; } = new[]
    {
        new MovementTypeOption(MovementType.Entry, "Entrada"),
        new MovementTypeOption(MovementType.Exit, "Salida")
    };

    /// <summary>
    /// Gets the catalog of products available for selection.
    /// </summary>
    public ObservableCollection<Product> Products { get; } = new();

    /// <summary>
    /// Gets the dialog window title matching the currently selected movement type.
    /// </summary>
    public string Title => SelectedMovementType switch
    {
        MovementType.Entry => "Registrar Entrada",
        MovementType.Exit => "Registrar Salida",
        _ => "Registrar Movimiento"
    };

    /// <summary>
    /// Gets a value indicating whether the current product stock indicator should be shown.
    /// </summary>
    public bool ShowAvailableStock => SelectedMovementType == MovementType.Exit && SelectedProduct != null;

    /// <summary>
    /// Gets the current global stock of the selected product.
    /// </summary>
    public int AvailableStock => ShowAvailableStock
        ? _inventoryService.GetTotalStockByProduct(SelectedProduct!.Id)
        : 0;

    /// <summary>
    /// Raised when the form requests closing with the dialog outcome (true = registered, false = cancelled).
    /// </summary>
    public event Action<bool>? RequestClose;

    /// <summary>
    /// Initializes a new instance of <see cref="MovementFormViewModel"/> preselected with the given movement type.
    /// </summary>
    public MovementFormViewModel(
        IInventoryService inventoryService,
        ICatalogService catalogService,
        MovementType initialMovementType = MovementType.Entry)
    {
        _inventoryService = inventoryService;
        _selectedMovementType = initialMovementType;

        foreach (var product in catalogService.GetAllProducts())
        {
            Products.Add(product);
        }
    }

    /// <summary>
    /// Updates dependent title and stock indicator when the movement type changes.
    /// </summary>
    partial void OnSelectedMovementTypeChanged(MovementType value)
    {
        FormError = string.Empty;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ShowAvailableStock));
        OnPropertyChanged(nameof(AvailableStock));
    }

    /// <summary>
    /// Revalidates the selected product and refreshes the available stock indicator.
    /// </summary>
    partial void OnSelectedProductChanged(Product? value)
    {
        FormError = string.Empty;
        ValidateProduct();
        OnPropertyChanged(nameof(ShowAvailableStock));
        OnPropertyChanged(nameof(AvailableStock));
    }

    /// <summary>
    /// Revalidates the quantity text whenever it changes.
    /// </summary>
    partial void OnQuantityTextChanged(string value)
    {
        FormError = string.Empty;
        ValidateQuantity();
    }

    /// <summary>
    /// Validates all active fields and submits the movement through <see cref="IInventoryService"/>, keeping the modal open on errors.
    /// </summary>
    [RelayCommand]
    public void Save()
    {
        FormError = string.Empty;
        ValidateAll();

        if (HasErrors || SelectedProduct == null || !TryParsePositiveQuantity(QuantityText, out var quantity))
            return;

        var request = new StockMovementRequest(
            SelectedProduct.Id,
            SelectedMovementType,
            quantity,
            Notes?.Trim() ?? string.Empty);

        try
        {
            _inventoryService.RegisterMovement(request);
            RequestClose?.Invoke(true);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            FormError = ex.Message;
        }
    }

    /// <summary>
    /// Cancels registration and closes the dialog with a false result.
    /// </summary>
    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    /// <summary>
    /// Runs validation across all fields in the movement form.
    /// </summary>
    private void ValidateAll()
    {
        ValidateProduct();
        ValidateQuantity();
    }

    /// <summary>
    /// Validates that a product has been selected.
    /// </summary>
    private void ValidateProduct()
    {
        var error = SelectedProduct == null
            ? "Debe seleccionar un producto."
            : string.Empty;

        ProductError = error;
        SetPropertyError(nameof(SelectedProduct), error);
    }

    /// <summary>
    /// Validates that the quantity parses as a strictly positive integer.
    /// </summary>
    private void ValidateQuantity()
    {
        var error = TryParsePositiveQuantity(QuantityText, out _)
            ? string.Empty
            : "Ingrese una cantidad entera mayor a 0.";

        QuantityError = error;
        SetPropertyError(nameof(QuantityText), error);
    }

    /// <summary>
    /// Parses a strictly positive integer quantity from text input.
    /// </summary>
    private static bool TryParsePositiveQuantity(string? text, out int quantity)
    {
        quantity = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity)
            && quantity > 0;
    }
}

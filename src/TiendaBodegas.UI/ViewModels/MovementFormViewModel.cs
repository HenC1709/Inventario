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
/// Modal form ViewModel for registering inventory entries, exits, and transfers with <see cref="System.ComponentModel.INotifyDataErrorInfo"/> validation.
/// </summary>
public partial class MovementFormViewModel : ValidatableViewModel
{
    private readonly IInventoryService _inventoryService;

    [ObservableProperty]
    private MovementType _selectedMovementType;

    [ObservableProperty]
    private Product? _selectedProduct;

    [ObservableProperty]
    private Warehouse? _selectedSourceWarehouse;

    [ObservableProperty]
    private Warehouse? _selectedDestinationWarehouse;

    [ObservableProperty]
    private string _quantityText = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _productError = string.Empty;

    [ObservableProperty]
    private string _sourceWarehouseError = string.Empty;

    [ObservableProperty]
    private string _destinationWarehouseError = string.Empty;

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
        new MovementTypeOption(MovementType.Exit, "Salida"),
        new MovementTypeOption(MovementType.Transfer, "Traslado")
    };

    /// <summary>
    /// Gets the catalog of products available for selection.
    /// </summary>
    public ObservableCollection<Product> Products { get; } = new();

    /// <summary>
    /// Gets the catalog of warehouses available for selection.
    /// </summary>
    public ObservableCollection<Warehouse> Warehouses { get; } = new();

    /// <summary>
    /// Gets the dialog window title matching the currently selected movement type.
    /// </summary>
    public string Title => SelectedMovementType switch
    {
        MovementType.Entry => "Registrar Entrada",
        MovementType.Exit => "Registrar Salida",
        MovementType.Transfer => "Registrar Traslado",
        _ => "Registrar Movimiento"
    };

    /// <summary>
    /// Gets a value indicating whether the selected movement type requires a source warehouse.
    /// </summary>
    public bool RequiresSourceWarehouse => SelectedMovementType is MovementType.Exit or MovementType.Transfer;

    /// <summary>
    /// Gets a value indicating whether the selected movement type requires a destination warehouse.
    /// </summary>
    public bool RequiresDestinationWarehouse => SelectedMovementType is MovementType.Entry or MovementType.Transfer;

    /// <summary>
    /// Gets a value indicating whether the source warehouse stock indicator should be shown.
    /// </summary>
    public bool ShowAvailableStock => RequiresSourceWarehouse && SelectedProduct != null && SelectedSourceWarehouse != null;

    /// <summary>
    /// Gets the current stock of the selected product in the selected source warehouse.
    /// </summary>
    public int AvailableSourceStock => ShowAvailableStock
        ? _inventoryService.GetProductStockInWarehouse(SelectedProduct!.Id, SelectedSourceWarehouse!.Id)
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

        foreach (var warehouse in catalogService.GetAllWarehouses())
        {
            Warehouses.Add(warehouse);
        }
    }

    /// <summary>
    /// Updates dependent visibility flags, title, stock preview, and field validation when the movement type changes.
    /// </summary>
    partial void OnSelectedMovementTypeChanged(MovementType value)
    {
        FormError = string.Empty;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(RequiresSourceWarehouse));
        OnPropertyChanged(nameof(RequiresDestinationWarehouse));
        OnPropertyChanged(nameof(ShowAvailableStock));
        OnPropertyChanged(nameof(AvailableSourceStock));

        if (!RequiresSourceWarehouse)
        {
            SourceWarehouseError = string.Empty;
            SetPropertyError(nameof(SelectedSourceWarehouse), null);
        }
        else if (SelectedSourceWarehouse != null)
        {
            ValidateSourceWarehouse();
        }

        if (!RequiresDestinationWarehouse)
        {
            DestinationWarehouseError = string.Empty;
            SetPropertyError(nameof(SelectedDestinationWarehouse), null);
        }
        else if (SelectedDestinationWarehouse != null)
        {
            ValidateDestinationWarehouse();
        }
    }

    /// <summary>
    /// Revalidates the selected product and refreshes the available stock indicator.
    /// </summary>
    partial void OnSelectedProductChanged(Product? value)
    {
        FormError = string.Empty;
        ValidateProduct();
        OnPropertyChanged(nameof(ShowAvailableStock));
        OnPropertyChanged(nameof(AvailableSourceStock));
    }

    /// <summary>
    /// Revalidates the source warehouse (and distinct-warehouse rule on transfers) and refreshes the stock indicator.
    /// </summary>
    partial void OnSelectedSourceWarehouseChanged(Warehouse? value)
    {
        FormError = string.Empty;
        ValidateSourceWarehouse();
        if (SelectedMovementType == MovementType.Transfer && SelectedDestinationWarehouse != null)
        {
            ValidateDestinationWarehouse();
        }

        OnPropertyChanged(nameof(ShowAvailableStock));
        OnPropertyChanged(nameof(AvailableSourceStock));
    }

    /// <summary>
    /// Revalidates the destination warehouse whenever it changes.
    /// </summary>
    partial void OnSelectedDestinationWarehouseChanged(Warehouse? value)
    {
        FormError = string.Empty;
        ValidateDestinationWarehouse();
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
            RequiresSourceWarehouse ? SelectedSourceWarehouse?.Id : null,
            RequiresDestinationWarehouse ? SelectedDestinationWarehouse?.Id : null,
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
    /// Runs validation across all fields applicable to the current movement type.
    /// </summary>
    private void ValidateAll()
    {
        ValidateProduct();
        ValidateSourceWarehouse();
        ValidateDestinationWarehouse();
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
    /// Validates that a source warehouse is selected when required by the movement type.
    /// </summary>
    private void ValidateSourceWarehouse()
    {
        var error = RequiresSourceWarehouse && SelectedSourceWarehouse == null
            ? "Debe seleccionar la bodega de origen."
            : string.Empty;

        SourceWarehouseError = error;
        SetPropertyError(nameof(SelectedSourceWarehouse), error);
    }

    /// <summary>
    /// Validates destination warehouse selection and ensures origin and destination differ on transfers.
    /// </summary>
    private void ValidateDestinationWarehouse()
    {
        string error;
        if (!RequiresDestinationWarehouse)
        {
            error = string.Empty;
        }
        else if (SelectedDestinationWarehouse == null)
        {
            error = "Debe seleccionar la bodega de destino.";
        }
        else if (SelectedMovementType == MovementType.Transfer
            && SelectedSourceWarehouse != null
            && SelectedSourceWarehouse.Id == SelectedDestinationWarehouse.Id)
        {
            error = "La bodega de origen y la de destino deben ser diferentes.";
        }
        else
        {
            error = string.Empty;
        }

        DestinationWarehouseError = error;
        SetPropertyError(nameof(SelectedDestinationWarehouse), error);
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

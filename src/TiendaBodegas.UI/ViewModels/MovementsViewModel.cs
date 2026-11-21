using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Enums;
using TiendaBodegas.UI.Services;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// ViewModel managing the filterable inventory movement history and entry/exit registration actions.
/// </summary>
public partial class MovementsViewModel : ObservableObject
{
    private const string InvalidDateRangeMessage = "La fecha 'Desde' no puede ser posterior a la fecha 'Hasta'.";

    private readonly IInventoryService _inventoryService;
    private readonly ICatalogService _catalogService;
    private readonly IDialogService _dialogService;
    private bool _isUpdatingFilters;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isErrorMessage;

    [ObservableProperty]
    private DateTime? _fromDate;

    [ObservableProperty]
    private DateTime? _toDate;

    [ObservableProperty]
    private MovementTypeFilterOption _selectedMovementTypeFilter;

    /// <summary>
    /// Gets the selectable movement type filter options including "Todos".
    /// </summary>
    public IReadOnlyList<MovementTypeFilterOption> MovementTypeFilters { get; } = new[]
    {
        new MovementTypeFilterOption(null, "Todos"),
        new MovementTypeFilterOption(MovementType.Entry, "Entrada"),
        new MovementTypeFilterOption(MovementType.Exit, "Salida")
    };

    /// <summary>
    /// Gets the observable collection of enriched stock movements bound to the UI.
    /// </summary>
    public ObservableCollection<StockMovementDisplayDto> Movements { get; } = new();

    /// <summary>
    /// Initializes a new instance of <see cref="MovementsViewModel"/> and loads initial data.
    /// </summary>
    public MovementsViewModel(
        IInventoryService inventoryService,
        ICatalogService catalogService,
        IDialogService dialogService)
    {
        _inventoryService = inventoryService;
        _catalogService = catalogService;
        _dialogService = dialogService;
        _selectedMovementTypeFilter = MovementTypeFilters[0];
        LoadData();
    }

    /// <summary>
    /// Reloads movement history using the currently active filters.
    /// </summary>
    public void LoadData()
    {
        ApplyFilters();
    }

    /// <summary>
    /// Clears all active history filters and reloads all movements.
    /// </summary>
    [RelayCommand]
    public void ClearFilters()
    {
        _isUpdatingFilters = true;
        try
        {
            FromDate = null;
            ToDate = null;
            SelectedMovementTypeFilter = MovementTypeFilters[0];
        }
        finally
        {
            _isUpdatingFilters = false;
        }

        ApplyFilters();
    }

    /// <summary>
    /// Opens the modal form preconfigured for a stock entry.
    /// </summary>
    [RelayCommand]
    public void RegisterEntry()
    {
        OpenMovementForm(MovementType.Entry, "Entrada de inventario registrada correctamente.");
    }

    /// <summary>
    /// Opens the modal form preconfigured for a stock exit.
    /// </summary>
    [RelayCommand]
    public void RegisterExit()
    {
        OpenMovementForm(MovementType.Exit, "Salida de inventario registrada correctamente.");
    }

    /// <summary>
    /// Reapplies history filters when the start date changes.
    /// </summary>
    partial void OnFromDateChanged(DateTime? value)
    {
        if (!_isUpdatingFilters)
            ApplyFilters();
    }

    /// <summary>
    /// Reapplies history filters when the end date changes.
    /// </summary>
    partial void OnToDateChanged(DateTime? value)
    {
        if (!_isUpdatingFilters)
            ApplyFilters();
    }

    /// <summary>
    /// Reapplies history filters when the movement type filter changes.
    /// </summary>
    partial void OnSelectedMovementTypeFilterChanged(MovementTypeFilterOption value)
    {
        if (!_isUpdatingFilters)
            ApplyFilters();
    }

    /// <summary>
    /// Validates date range and loads filtered movement details from <see cref="IInventoryService"/>.
    /// </summary>
    private void ApplyFilters()
    {
        if (FromDate.HasValue && ToDate.HasValue && FromDate.Value.Date > ToDate.Value.Date)
        {
            Movements.Clear();
            SetErrorStatus(InvalidDateRangeMessage);
            return;
        }

        if (IsErrorMessage && StatusMessage == InvalidDateRangeMessage)
        {
            ClearStatus();
        }

        var filter = new StockMovementFilter(
            FromDate,
            ToDate,
            SelectedMovementTypeFilter?.Type);

        Movements.Clear();
        foreach (var movement in _inventoryService.GetMovementDetails(filter))
        {
            Movements.Add(movement);
        }
    }

    /// <summary>
    /// Validates catalog prerequisites, opens the movement registration modal, and updates status feedback on completion.
    /// </summary>
    private void OpenMovementForm(MovementType movementType, string successMessage)
    {
        ClearStatus();

        if (!_catalogService.GetAllProducts().Any())
        {
            SetErrorStatus("Debe registrar al menos un producto antes de crear movimientos.");
            return;
        }

        var formViewModel = new MovementFormViewModel(_inventoryService, _catalogService, movementType);
        if (_dialogService.ShowMovementForm(formViewModel))
        {
            LoadData();
            SetSuccessStatus(successMessage);
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

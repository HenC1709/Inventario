using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.UI.Services;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// ViewModel managing the searchable warehouse list and CRUD actions.
/// </summary>
public partial class WarehousesViewModel : ObservableObject
{
    private readonly ICatalogService _catalogService;
    private readonly IDialogService _dialogService;
    private List<Warehouse> _allWarehouses = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditWarehouseCommand), nameof(DeleteWarehouseCommand))]
    private Warehouse? _selectedWarehouse;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isErrorMessage;

    /// <summary>
    /// Gets the filtered observable collection of warehouses bound to the UI.
    /// </summary>
    public ObservableCollection<Warehouse> Warehouses { get; } = new();

    /// <summary>
    /// Initializes a new instance of <see cref="WarehousesViewModel"/> and loads initial data.
    /// </summary>
    public WarehousesViewModel(ICatalogService catalogService, IDialogService dialogService)
    {
        _catalogService = catalogService;
        _dialogService = dialogService;
        LoadData();
    }

    /// <summary>
    /// Reloads all warehouses from the catalog service and applies the current search filter.
    /// </summary>
    public void LoadData()
    {
        _allWarehouses = _catalogService.GetAllWarehouses().ToList();
        ApplyFilter();
    }

    /// <summary>
    /// Opens the modal form to create a new warehouse.
    /// </summary>
    [RelayCommand]
    public void CreateWarehouse()
    {
        ClearStatus();
        var formViewModel = new WarehouseFormViewModel(_catalogService);
        if (_dialogService.ShowWarehouseForm(formViewModel))
        {
            LoadData();
            SetSuccessStatus("Bodega creada correctamente.");
        }
    }

    /// <summary>
    /// Opens the modal form to edit the currently selected warehouse.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedWarehouse))]
    public void EditWarehouse()
    {
        if (SelectedWarehouse == null)
            return;

        ClearStatus();
        var formViewModel = new WarehouseFormViewModel(_catalogService, SelectedWarehouse);
        if (_dialogService.ShowWarehouseForm(formViewModel))
        {
            LoadData();
            SetSuccessStatus("Bodega actualizada correctamente.");
        }
    }

    /// <summary>
    /// Requests confirmation and deletes the selected warehouse when permitted by business rules.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedWarehouse))]
    public void DeleteWarehouse()
    {
        if (SelectedWarehouse == null)
            return;

        ClearStatus();
        var warehouse = SelectedWarehouse;
        if (!_dialogService.Confirm($"¿Desea eliminar la bodega '{warehouse.Name}'?", "Confirmar eliminación"))
            return;

        try
        {
            _catalogService.DeleteWarehouse(warehouse.Id);
            SelectedWarehouse = null;
            LoadData();
            SetSuccessStatus("Bodega eliminada correctamente.");
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
    private bool HasSelectedWarehouse() => SelectedWarehouse != null;

    /// <summary>
    /// Filters the cached warehouse list by name or address (case-insensitive).
    /// </summary>
    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        var filtered = string.IsNullOrEmpty(term)
            ? _allWarehouses
            : _allWarehouses.Where(warehouse =>
                warehouse.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                warehouse.Address.Contains(term, StringComparison.OrdinalIgnoreCase));

        Warehouses.Clear();
        foreach (var warehouse in filtered)
        {
            Warehouses.Add(warehouse);
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

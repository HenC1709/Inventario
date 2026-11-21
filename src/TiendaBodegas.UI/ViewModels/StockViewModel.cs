using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// ViewModel displaying per-warehouse stock balances, global product totals, and low-stock alerts.
/// </summary>
public partial class StockViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly ICatalogService _catalogService;
    private List<WarehouseStockDto> _allWarehouseStock = new();
    private List<ProductStockSummaryDto> _allProductTotals = new();
    private bool _isUpdatingFilters;

    [ObservableProperty]
    private WarehouseFilterOption? _selectedWarehouseFilter;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showOnlyLowStock;

    [ObservableProperty]
    private int _lowStockCount;

    [ObservableProperty]
    private int _lowTotalProductCount;

    /// <summary>
    /// Gets the selectable warehouse filter options including "Todas las bodegas".
    /// </summary>
    public ObservableCollection<WarehouseFilterOption> WarehouseFilters { get; } = new();

    /// <summary>
    /// Gets the filtered per-warehouse stock records bound to the UI.
    /// </summary>
    public ObservableCollection<WarehouseStockDto> StockItems { get; } = new();

    /// <summary>
    /// Gets the filtered global stock totals per product bound to the UI.
    /// </summary>
    public ObservableCollection<ProductStockSummaryDto> ProductTotals { get; } = new();

    /// <summary>
    /// Gets a value indicating whether any warehouse stock or global product total is at or below minimum stock.
    /// </summary>
    public bool HasLowStockAlerts => LowStockCount > 0 || LowTotalProductCount > 0;

    /// <summary>
    /// Gets the localized summary message describing the current low-stock alert status.
    /// </summary>
    public string AlertSummaryText => HasLowStockAlerts
        ? $"Alerta de stock bajo: {LowStockCount} registro(s) por bodega y {LowTotalProductCount} producto(s) en total por debajo o igual al mínimo."
        : "Todas las existencias están por encima del stock mínimo.";

    /// <summary>
    /// Initializes a new instance of <see cref="StockViewModel"/> and loads initial stock data.
    /// </summary>
    public StockViewModel(IInventoryService inventoryService, ICatalogService catalogService)
    {
        _inventoryService = inventoryService;
        _catalogService = catalogService;
        LoadData();
    }

    /// <summary>
    /// Reloads warehouses, per-warehouse balances, and global product totals, preserving active filters.
    /// </summary>
    public void LoadData()
    {
        _isUpdatingFilters = true;
        try
        {
            var previousWarehouseId = SelectedWarehouseFilter?.WarehouseId;
            WarehouseFilters.Clear();
            WarehouseFilters.Add(new WarehouseFilterOption(null, "Todas las bodegas"));
            foreach (var warehouse in _catalogService.GetAllWarehouses())
            {
                WarehouseFilters.Add(new WarehouseFilterOption(warehouse.Id, warehouse.Name));
            }

            SelectedWarehouseFilter = previousWarehouseId.HasValue
                ? WarehouseFilters.FirstOrDefault(w => w.WarehouseId == previousWarehouseId.Value) ?? WarehouseFilters.FirstOrDefault()
                : WarehouseFilters.FirstOrDefault();

            _allWarehouseStock = _inventoryService.GetWarehouseStockReport().ToList();
            _allProductTotals = _inventoryService.GetProductStockSummary().ToList();
        }
        finally
        {
            _isUpdatingFilters = false;
        }

        ApplyFilters();
    }

    /// <summary>
    /// Resets all active stock filters and refreshes both grids.
    /// </summary>
    [RelayCommand]
    public void ClearFilters()
    {
        _isUpdatingFilters = true;
        try
        {
            SelectedWarehouseFilter = WarehouseFilters.FirstOrDefault();
            SearchText = string.Empty;
            ShowOnlyLowStock = false;
        }
        finally
        {
            _isUpdatingFilters = false;
        }

        ApplyFilters();
    }

    /// <summary>
    /// Reapplies stock filters when the selected warehouse changes.
    /// </summary>
    partial void OnSelectedWarehouseFilterChanged(WarehouseFilterOption? value)
    {
        if (!_isUpdatingFilters)
            ApplyFilters();
    }

    /// <summary>
    /// Reapplies stock filters when the search text changes.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        if (!_isUpdatingFilters)
            ApplyFilters();
    }

    /// <summary>
    /// Reapplies stock filters when the low-stock toggle changes.
    /// </summary>
    partial void OnShowOnlyLowStockChanged(bool value)
    {
        if (!_isUpdatingFilters)
            ApplyFilters();
    }

    /// <summary>
    /// Applies warehouse, search, and low-stock filters to both stock collections and updates alert counters.
    /// </summary>
    private void ApplyFilters()
    {
        var selectedWarehouseId = SelectedWarehouseFilter?.WarehouseId;
        var term = SearchText.Trim();

        var scopedWarehouseItems = _allWarehouseStock
            .Where(item => !selectedWarehouseId.HasValue || item.WarehouseId == selectedWarehouseId.Value)
            .Where(item => string.IsNullOrEmpty(term)
                || item.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.ProductSku.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.WarehouseName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();

        LowStockCount = scopedWarehouseItems.Count(item => item.IsLowStock);

        var visibleWarehouseItems = ShowOnlyLowStock
            ? scopedWarehouseItems.Where(item => item.IsLowStock)
            : scopedWarehouseItems;

        StockItems.Clear();
        foreach (var item in visibleWarehouseItems)
        {
            StockItems.Add(item);
        }

        var scopedProductTotals = _allProductTotals
            .Where(item => string.IsNullOrEmpty(term)
                || item.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.ProductSku.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();

        LowTotalProductCount = scopedProductTotals.Count(item => item.IsLowStock);

        var visibleProductTotals = ShowOnlyLowStock
            ? scopedProductTotals.Where(item => item.IsLowStock)
            : scopedProductTotals;

        ProductTotals.Clear();
        foreach (var item in visibleProductTotals)
        {
            ProductTotals.Add(item);
        }

        OnPropertyChanged(nameof(HasLowStockAlerts));
        OnPropertyChanged(nameof(AlertSummaryText));
    }
}

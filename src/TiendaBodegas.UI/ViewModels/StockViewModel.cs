using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// ViewModel displaying global product stock totals and low-stock alerts for the single-warehouse inventory.
/// </summary>
public partial class StockViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private List<ProductStockSummaryDto> _allProductTotals = new();
    private bool _isUpdatingFilters;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showOnlyLowStock;

    [ObservableProperty]
    private int _lowTotalProductCount;

    /// <summary>
    /// Gets the filtered global stock totals per product bound to the UI.
    /// </summary>
    public ObservableCollection<ProductStockSummaryDto> ProductTotals { get; } = new();

    /// <summary>
    /// Gets a value indicating whether any product total is at or below its minimum stock threshold.
    /// </summary>
    public bool HasLowStockAlerts => LowTotalProductCount > 0;

    /// <summary>
    /// Gets the localized summary message describing the current low-stock alert status.
    /// </summary>
    public string AlertSummaryText => HasLowStockAlerts
        ? $"Alerta de stock bajo: {LowTotalProductCount} producto(s) por debajo o igual al mínimo."
        : "Todas las existencias están por encima del stock mínimo.";

    /// <summary>
    /// Initializes a new instance of <see cref="StockViewModel"/> and loads initial stock data.
    /// </summary>
    public StockViewModel(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
        LoadData();
    }

    /// <summary>
    /// Reloads global product stock balances and reapplies active filters.
    /// </summary>
    public void LoadData()
    {
        _allProductTotals = _inventoryService.GetProductStockSummary().ToList();
        ApplyFilters();
    }

    /// <summary>
    /// Resets all active stock filters and refreshes the product grid.
    /// </summary>
    [RelayCommand]
    public void ClearFilters()
    {
        _isUpdatingFilters = true;
        try
        {
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
    /// Applies search and low-stock filters to <see cref="ProductTotals"/> and updates alert counters.
    /// </summary>
    private void ApplyFilters()
    {
        var term = SearchText.Trim();

        var scopedProductTotals = _allProductTotals
            .Where(item => string.IsNullOrEmpty(term)
                || item.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.ProductSku.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.Category.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.SupplierName.Contains(term, StringComparison.OrdinalIgnoreCase))
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

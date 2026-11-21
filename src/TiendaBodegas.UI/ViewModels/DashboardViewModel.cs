using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiendaBodegas.Application.Interfaces;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// Display item representing a category's share of total inventory units for dashboard progress bars.
/// </summary>
public sealed record CategoryStockBarItem(string Category, int TotalUnits, double Percentage, string UnitsLabel);

/// <summary>
/// Display item representing a critical low-stock or out-of-stock product alert on the dashboard.
/// </summary>
public sealed record StockAlertItem(int ProductId, string ProductName, int TotalStock, string UnitsLabel);

/// <summary>
/// ViewModel powering the executive dashboard with category stock distribution bars and replenishment alerts.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private const int MaxCriticalAlertsDisplayed = 5;

    private readonly IInventoryService _inventoryService;

    [ObservableProperty]
    private int _totalAlertsCount;

    /// <summary>
    /// Gets the stock distribution rows per category ordered from highest to lowest total units.
    /// </summary>
    public ObservableCollection<CategoryStockBarItem> CategoryDistributions { get; } = new();

    /// <summary>
    /// Gets the top critical products requiring replenishment.
    /// </summary>
    public ObservableCollection<StockAlertItem> CriticalAlerts { get; } = new();

    /// <summary>
    /// Gets a value indicating whether there are no categories to display.
    /// </summary>
    public bool HasNoCategories => CategoryDistributions.Count == 0;

    /// <summary>
    /// Gets a value indicating whether all inventory items are above minimum stock.
    /// </summary>
    public bool HasNoAlerts => CriticalAlerts.Count == 0;

    /// <summary>
    /// Raised when the user requests navigating to the full inventory view.
    /// </summary>
    public event Action? NavigateToInventoryRequested;

    /// <summary>
    /// Raised when the user requests navigating to the inventory view pre-filtered to low/out-of-stock items.
    /// </summary>
    public event Action? NavigateToCriticalAlertsRequested;

    /// <summary>
    /// Gets the page title displayed in the dashboard header.
    /// </summary>
    public string Title => "Dashboard General";

    /// <summary>
    /// Gets the subtitle describing the dashboard section.
    /// </summary>
    public string Subtitle => "Resumen ejecutivo del estado del almacén";

    /// <summary>
    /// Initializes a new instance of <see cref="DashboardViewModel"/> and loads initial summary data.
    /// </summary>
    public DashboardViewModel(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
        LoadData();
    }

    /// <summary>
    /// Refreshes category stock distributions and critical replenishment alerts from <see cref="IInventoryService"/>.
    /// </summary>
    public void LoadData()
    {
        var items = _inventoryService.GetProductStockSummary().ToList();

        var groupedCategories = items
            .GroupBy(item => string.IsNullOrWhiteSpace(item.Category) ? "Sin categoría" : item.Category.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => (Category: group.Key, TotalUnits: group.Sum(i => i.TotalStock)))
            .OrderByDescending(entry => entry.TotalUnits)
            .ThenBy(entry => entry.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var maxUnits = groupedCategories.Count > 0
            ? Math.Max(1, groupedCategories.Max(c => c.TotalUnits))
            : 1;

        CategoryDistributions.Clear();
        foreach (var entry in groupedCategories)
        {
            var percentage = Math.Round(entry.TotalUnits * 100.0 / maxUnits, 1);
            CategoryDistributions.Add(new CategoryStockBarItem(
                entry.Category,
                entry.TotalUnits,
                percentage,
                $"{entry.TotalUnits} uds."));
        }

        var alertProducts = items
            .Where(item => item.IsLowStock || item.IsOutOfStock)
            .OrderBy(item => item.TotalStock)
            .ThenBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        TotalAlertsCount = alertProducts.Count;

        CriticalAlerts.Clear();
        foreach (var item in alertProducts.Take(MaxCriticalAlertsDisplayed))
        {
            CriticalAlerts.Add(new StockAlertItem(
                item.ProductId,
                item.ProductName,
                item.TotalStock,
                $"{item.TotalStock} uds."));
        }

        OnPropertyChanged(nameof(HasNoCategories));
        OnPropertyChanged(nameof(HasNoAlerts));
    }

    /// <summary>
    /// Requests navigation to the Inventory section without active alert filtering.
    /// </summary>
    [RelayCommand]
    public void GoToInventory()
    {
        NavigateToInventoryRequested?.Invoke();
    }

    /// <summary>
    /// Requests navigation to the Inventory section filtered to critical low-stock products.
    /// </summary>
    [RelayCommand]
    public void ReviewCriticalProducts()
    {
        NavigateToCriticalAlertsRequested?.Invoke();
    }
}

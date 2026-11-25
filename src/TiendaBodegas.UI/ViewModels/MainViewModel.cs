using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiendaBodegas.Application.Interfaces;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// Root navigation ViewModel coordinating section switching, active indicators, and sidebar badge count.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly InventoryViewModel _inventoryViewModel;
    private readonly SuppliersViewModel _suppliersViewModel;
    private readonly MovementsViewModel _movementsViewModel;
    private readonly ReportsViewModel _reportsViewModel;
    private readonly SettingsViewModel _settingsViewModel;

    [ObservableProperty]
    private object _currentViewModel;

    [ObservableProperty]
    private string _currentSectionTitle = "Inventario";

    [ObservableProperty]
    private NavigationSection _activeSection = NavigationSection.Inventory;

    [ObservableProperty]
    private int _inventoryCount;

    /// <summary>Gets whether the Dashboard section is currently active.</summary>
    public bool IsDashboardActive => ActiveSection == NavigationSection.Dashboard;

    /// <summary>Gets whether the Inventory section is currently active.</summary>
    public bool IsInventoryActive => ActiveSection == NavigationSection.Inventory;

    /// <summary>Gets whether the Suppliers section is currently active.</summary>
    public bool IsSuppliersActive => ActiveSection == NavigationSection.Suppliers;

    /// <summary>Gets whether the Sales/Movements section is currently active.</summary>
    public bool IsSalesActive => ActiveSection == NavigationSection.Sales;

    /// <summary>Gets whether the Reports section is currently active.</summary>
    public bool IsReportsActive => ActiveSection == NavigationSection.Reports;

    /// <summary>Gets whether the Settings section is currently active.</summary>
    public bool IsSettingsActive => ActiveSection == NavigationSection.Settings;

    /// <summary>
    /// Initializes a new instance of <see cref="MainViewModel"/> with the unified Inventory section active by default.
    /// </summary>
    public MainViewModel(
        DashboardViewModel dashboardViewModel,
        InventoryViewModel inventoryViewModel,
        SuppliersViewModel suppliersViewModel,
        MovementsViewModel movementsViewModel,
        ReportsViewModel reportsViewModel,
        SettingsViewModel settingsViewModel,
        IDatabaseBackupService? backupService = null)
    {
        _dashboardViewModel = dashboardViewModel;
        _inventoryViewModel = inventoryViewModel;
        _suppliersViewModel = suppliersViewModel;
        _movementsViewModel = movementsViewModel;
        _reportsViewModel = reportsViewModel;
        _settingsViewModel = settingsViewModel;

        _currentViewModel = _inventoryViewModel;
        _inventoryCount = _inventoryViewModel.TotalProductsCount;
        _inventoryViewModel.TotalProductCountChanged += count => InventoryCount = count;
        _dashboardViewModel.NavigateToInventoryRequested += ShowInventory;
        _dashboardViewModel.NavigateToCriticalAlertsRequested += ShowInventoryLowStock;

        if (backupService != null)
            backupService.DatabaseRestored += OnDatabaseRestored;
    }

    partial void OnActiveSectionChanged(NavigationSection value)
    {
        OnPropertyChanged(nameof(IsDashboardActive));
        OnPropertyChanged(nameof(IsInventoryActive));
        OnPropertyChanged(nameof(IsSuppliersActive));
        OnPropertyChanged(nameof(IsSalesActive));
        OnPropertyChanged(nameof(IsReportsActive));
        OnPropertyChanged(nameof(IsSettingsActive));
    }

    /// <summary>
    /// Navigates to the Dashboard section and refreshes its stock distribution and alert widgets.
    /// </summary>
    [RelayCommand]
    public void ShowDashboard()
    {
        _dashboardViewModel.LoadData();
        ActiveSection = NavigationSection.Dashboard;
        CurrentViewModel = _dashboardViewModel;
        CurrentSectionTitle = "Dashboard";
    }

    /// <summary>
    /// Navigates to the unified Inventory section and refreshes its data and badge counter.
    /// </summary>
    [RelayCommand]
    public void ShowInventory()
    {
        _inventoryViewModel.LoadData();
        InventoryCount = _inventoryViewModel.TotalProductsCount;
        ActiveSection = NavigationSection.Inventory;
        CurrentViewModel = _inventoryViewModel;
        CurrentSectionTitle = "Inventario";
    }

    /// <summary>
    /// Navigates to the unified Inventory section filtered to critical low-stock and out-of-stock products.
    /// </summary>
    [RelayCommand]
    public void ShowInventoryLowStock()
    {
        _inventoryViewModel.LoadData();
        _inventoryViewModel.FilterLowStock();
        InventoryCount = _inventoryViewModel.TotalProductsCount;
        ActiveSection = NavigationSection.Inventory;
        CurrentViewModel = _inventoryViewModel;
        CurrentSectionTitle = "Inventario";
    }

    /// <summary>
    /// Navigates to the Suppliers directory section and refreshes its data.
    /// </summary>
    [RelayCommand]
    public void ShowSuppliers()
    {
        _suppliersViewModel.LoadData();
        ActiveSection = NavigationSection.Suppliers;
        CurrentViewModel = _suppliersViewModel;
        CurrentSectionTitle = "Proveedores";
    }

    /// <summary>
    /// Navigates to the Sales and Movements section and refreshes its data.
    /// </summary>
    [RelayCommand]
    public void ShowSales()
    {
        _movementsViewModel.LoadData();
        ActiveSection = NavigationSection.Sales;
        CurrentViewModel = _movementsViewModel;
        CurrentSectionTitle = "Ventas";
    }

    /// <summary>
    /// Navigates to the Reports section and resets stale banners.
    /// </summary>
    [RelayCommand]
    public void ShowReports()
    {
        _reportsViewModel.LoadData();
        ActiveSection = NavigationSection.Reports;
        CurrentViewModel = _reportsViewModel;
        CurrentSectionTitle = "Reportes";
    }

    /// <summary>
    /// Navigates to the System Settings section and reloads persisted preferences.
    /// </summary>
    [RelayCommand]
    public void ShowSettings()
    {
        _settingsViewModel.LoadData();
        ActiveSection = NavigationSection.Settings;
        CurrentViewModel = _settingsViewModel;
        CurrentSectionTitle = "Ajustes";
    }

    private void OnDatabaseRestored()
    {
        _settingsViewModel.LoadData();
        _inventoryViewModel.LoadData();
        _suppliersViewModel.LoadData();
        _movementsViewModel.LoadData();
        _dashboardViewModel.LoadData();
        _reportsViewModel.LoadData();
        InventoryCount = _inventoryViewModel.TotalProductsCount;
    }
}

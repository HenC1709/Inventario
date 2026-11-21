using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// Root navigation ViewModel coordinating section switching in the main application window.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly WarehousesViewModel _warehousesViewModel;
    private readonly ProductsViewModel _productsViewModel;
    private readonly StockViewModel _stockViewModel;
    private readonly MovementsViewModel _movementsViewModel;

    [ObservableProperty]
    private object _currentViewModel;

    [ObservableProperty]
    private string _currentSectionTitle = "Bodegas";

    /// <summary>
    /// Initializes a new instance of <see cref="MainViewModel"/> with the warehouses section active.
    /// </summary>
    public MainViewModel(
        WarehousesViewModel warehousesViewModel,
        ProductsViewModel productsViewModel,
        StockViewModel stockViewModel,
        MovementsViewModel movementsViewModel)
    {
        _warehousesViewModel = warehousesViewModel;
        _productsViewModel = productsViewModel;
        _stockViewModel = stockViewModel;
        _movementsViewModel = movementsViewModel;
        _currentViewModel = _warehousesViewModel;
    }

    /// <summary>
    /// Navigates to the Warehouses section and refreshes its data.
    /// </summary>
    [RelayCommand]
    public void ShowWarehouses()
    {
        _warehousesViewModel.LoadData();
        CurrentViewModel = _warehousesViewModel;
        CurrentSectionTitle = "Bodegas";
    }

    /// <summary>
    /// Navigates to the Products section and refreshes its data.
    /// </summary>
    [RelayCommand]
    public void ShowProducts()
    {
        _productsViewModel.LoadData();
        CurrentViewModel = _productsViewModel;
        CurrentSectionTitle = "Productos";
    }

    /// <summary>
    /// Navigates to the Stock by Warehouse section and refreshes stock balances and alerts.
    /// </summary>
    [RelayCommand]
    public void ShowStock()
    {
        _stockViewModel.LoadData();
        CurrentViewModel = _stockViewModel;
        CurrentSectionTitle = "Stock por Bodega";
    }

    /// <summary>
    /// Navigates to the Stock Movements section and refreshes its data.
    /// </summary>
    [RelayCommand]
    public void ShowMovements()
    {
        _movementsViewModel.LoadData();
        CurrentViewModel = _movementsViewModel;
        CurrentSectionTitle = "Movimientos";
    }
}

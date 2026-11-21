using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// Root navigation ViewModel coordinating section switching in the main application window.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly SuppliersViewModel _suppliersViewModel;
    private readonly ProductsViewModel _productsViewModel;
    private readonly StockViewModel _stockViewModel;
    private readonly MovementsViewModel _movementsViewModel;

    [ObservableProperty]
    private object _currentViewModel;

    [ObservableProperty]
    private string _currentSectionTitle = "Proveedores";

    /// <summary>
    /// Initializes a new instance of <see cref="MainViewModel"/> with the suppliers section active.
    /// </summary>
    public MainViewModel(
        SuppliersViewModel suppliersViewModel,
        ProductsViewModel productsViewModel,
        StockViewModel stockViewModel,
        MovementsViewModel movementsViewModel)
    {
        _suppliersViewModel = suppliersViewModel;
        _productsViewModel = productsViewModel;
        _stockViewModel = stockViewModel;
        _movementsViewModel = movementsViewModel;
        _currentViewModel = _suppliersViewModel;
    }

    /// <summary>
    /// Navigates to the Suppliers section and refreshes its data.
    /// </summary>
    [RelayCommand]
    public void ShowSuppliers()
    {
        _suppliersViewModel.LoadData();
        CurrentViewModel = _suppliersViewModel;
        CurrentSectionTitle = "Proveedores";
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
    /// Navigates to the global Stock section and refreshes stock balances and alerts.
    /// </summary>
    [RelayCommand]
    public void ShowStock()
    {
        _stockViewModel.LoadData();
        CurrentViewModel = _stockViewModel;
        CurrentSectionTitle = "Stock";
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

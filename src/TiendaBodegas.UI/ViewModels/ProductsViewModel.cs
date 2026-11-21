using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.UI.Services;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// ViewModel managing the searchable product catalog and CRUD actions.
/// </summary>
public partial class ProductsViewModel : ObservableObject
{
    private readonly ICatalogService _catalogService;
    private readonly IDialogService _dialogService;
    private List<Product> _allProducts = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditProductCommand), nameof(DeleteProductCommand))]
    private Product? _selectedProduct;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isErrorMessage;

    /// <summary>
    /// Gets the filtered observable collection of products bound to the UI.
    /// </summary>
    public ObservableCollection<Product> Products { get; } = new();

    /// <summary>
    /// Initializes a new instance of <see cref="ProductsViewModel"/> and loads initial data.
    /// </summary>
    public ProductsViewModel(ICatalogService catalogService, IDialogService dialogService)
    {
        _catalogService = catalogService;
        _dialogService = dialogService;
        LoadData();
    }

    /// <summary>
    /// Reloads all products from the catalog service and applies the current search filter.
    /// </summary>
    public void LoadData()
    {
        _allProducts = _catalogService.GetAllProducts().ToList();
        ApplyFilter();
    }

    /// <summary>
    /// Opens the modal form to create a new product.
    /// </summary>
    [RelayCommand]
    public void CreateProduct()
    {
        ClearStatus();
        var formViewModel = new ProductFormViewModel(_catalogService);
        if (_dialogService.ShowProductForm(formViewModel))
        {
            LoadData();
            SetSuccessStatus("Producto creado correctamente.");
        }
    }

    /// <summary>
    /// Opens the modal form to edit the currently selected product.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedProduct))]
    public void EditProduct()
    {
        if (SelectedProduct == null)
            return;

        ClearStatus();
        var formViewModel = new ProductFormViewModel(_catalogService, SelectedProduct);
        if (_dialogService.ShowProductForm(formViewModel))
        {
            LoadData();
            SetSuccessStatus("Producto actualizado correctamente.");
        }
    }

    /// <summary>
    /// Requests confirmation and deletes the selected product when permitted by business rules.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedProduct))]
    public void DeleteProduct()
    {
        if (SelectedProduct == null)
            return;

        ClearStatus();
        var product = SelectedProduct;
        if (!_dialogService.Confirm($"¿Desea eliminar el producto '{product.Name}' ({product.SKU})?", "Confirmar eliminación"))
            return;

        try
        {
            _catalogService.DeleteProduct(product.Id);
            SelectedProduct = null;
            LoadData();
            SetSuccessStatus("Producto eliminado correctamente.");
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
    private bool HasSelectedProduct() => SelectedProduct != null;

    /// <summary>
    /// Filters the cached product list by name, SKU, or category (case-insensitive).
    /// </summary>
    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        var filtered = string.IsNullOrEmpty(term)
            ? _allProducts
            : _allProducts.Where(product =>
                product.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                product.SKU.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                product.Category.Contains(term, StringComparison.OrdinalIgnoreCase));

        Products.Clear();
        foreach (var product in filtered)
        {
            Products.Add(product);
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

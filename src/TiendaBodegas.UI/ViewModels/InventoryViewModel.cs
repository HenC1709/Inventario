using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.UI.Services;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// Unified Inventory ViewModel providing live KPIs, multi-criteria filtering, cross-page sorting, pagination, CRUD, and CSV export.
/// </summary>
public partial class InventoryViewModel : ObservableObject
{
    public const int PageSize = 6;
    public const string StatusInStock = "En Stock";
    public const string StatusLowStock = "Bajo Stock";
    public const string StatusOutOfStock = "Sin Stock";
    public const string StatusAlerts = "Alertas";

    public const string SortSkuKey = "Sku";
    public const string SortNameKey = "Name";
    public const string SortCategoryKey = "Category";
    public const string SortPurchasePriceKey = "PurchasePrice";
    public const string SortSalePriceKey = "SalePrice";
    public const string SortStockKey = "Stock";

    private readonly ICatalogService _catalogService;
    private readonly IInventoryService _inventoryService;
    private readonly IInventoryExportService _exportService;
    private readonly IDialogService _dialogService;
    private readonly ISettingsService? _settingsService;

    private List<ProductStockSummaryDto> _allItems = new();
    private List<ProductStockSummaryDto> _filteredItems = new();
    private bool _isUpdatingFilters;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private StringFilterOption? _selectedCategoryFilter;

    [ObservableProperty]
    private StringFilterOption? _selectedStatusFilter;

    [ObservableProperty]
    private SupplierFilterOption? _selectedSupplierFilter;

    [ObservableProperty]
    private string? _sortColumn;

    [ObservableProperty]
    private bool _sortAscending = true;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private int _filteredItemsCount;

    [ObservableProperty]
    private int _totalProductsCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LowStockAlertBadgeText))]
    private int _lowAndOutOfStockCount;

    [ObservableProperty]
    private int _activeCategoriesCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedEstimatedValue))]
    private decimal _estimatedTotalValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedEstimatedValue))]
    private string _currencyCode = "USD";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedEstimatedValue))]
    private string _currencySymbol = "$";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isErrorMessage;

    /// <summary>
    /// Raised whenever the total number of catalog products changes so shell badges stay synchronized.
    /// </summary>
    public event Action<int>? TotalProductCountChanged;

    /// <summary>
    /// Gets the selectable category filter options including "Categoría: Todas".
    /// </summary>
    public ObservableCollection<StringFilterOption> CategoryFilters { get; } = new();

    /// <summary>
    /// Gets the selectable stock status filter options.
    /// </summary>
    public IReadOnlyList<StringFilterOption> StatusFilters { get; } = new[]
    {
        new StringFilterOption(null, "Estado: Todos"),
        new StringFilterOption(StatusInStock, StatusInStock),
        new StringFilterOption(StatusLowStock, StatusLowStock),
        new StringFilterOption(StatusOutOfStock, StatusOutOfStock),
        new StringFilterOption(StatusAlerts, "Bajo / Sin Stock")
    };

    /// <summary>
    /// Gets the selectable supplier filter options including "Proveedor: Todos".
    /// </summary>
    public ObservableCollection<SupplierFilterOption> SupplierFilters { get; } = new();

    /// <summary>
    /// Gets the current page slice of filtered and sorted inventory rows.
    /// </summary>
    public ObservableCollection<ProductStockSummaryDto> PagedItems { get; } = new();

    /// <summary>
    /// Gets the page index descriptors rendered in the pagination footer.
    /// </summary>
    public ObservableCollection<PageNumberItem> PageNumbers { get; } = new();

    /// <summary>
    /// Gets the total inventory sale valuation formatted with the active currency symbol.
    /// </summary>
    public string FormattedEstimatedValue =>
        CurrencySymbol + EstimatedTotalValue.ToString("#,##0.00", CultureInfo.GetCultureInfo("en-US"));

    /// <summary>
    /// Gets the attention badge caption shown inside the low-stock KPI card.
    /// </summary>
    public string LowStockAlertBadgeText => LowAndOutOfStockCount > 0 ? "¡Atención!" : "Óptimo";

    /// <summary>
    /// Gets the pagination summary text describing the visible item range.
    /// </summary>
    public string PaginationInfoText
    {
        get
        {
            if (FilteredItemsCount == 0)
                return "Mostrando 0 de 0 productos";

            var start = ((CurrentPage - 1) * PageSize) + 1;
            var end = Math.Min(CurrentPage * PageSize, FilteredItemsCount);
            return $"Mostrando {start}-{end} de {FilteredItemsCount} productos";
        }
    }

    /// <summary>
    /// Gets a value indicating whether the current filter criteria yielded zero matching products.
    /// </summary>
    public bool HasNoResults => FilteredItemsCount == 0;

    /// <summary>
    /// Gets a value indicating whether navigating to the previous page is available.
    /// </summary>
    public bool CanGoPrevious => CurrentPage > 1;

    /// <summary>
    /// Gets a value indicating whether navigating to the next page is available.
    /// </summary>
    public bool CanGoNext => CurrentPage < TotalPages;

    /// <summary>Gets the sort indicator glyph for the SKU column.</summary>
    public string SkuSortIndicator => GetSortIndicator(SortSkuKey);

    /// <summary>Gets the sort indicator glyph for the Product column.</summary>
    public string NameSortIndicator => GetSortIndicator(SortNameKey);

    /// <summary>Gets the sort indicator glyph for the Category column.</summary>
    public string CategorySortIndicator => GetSortIndicator(SortCategoryKey);

    /// <summary>Gets the sort indicator glyph for the Purchase Price column.</summary>
    public string PurchasePriceSortIndicator => GetSortIndicator(SortPurchasePriceKey);

    /// <summary>Gets the sort indicator glyph for the Sale Price column.</summary>
    public string SalePriceSortIndicator => GetSortIndicator(SortSalePriceKey);

    /// <summary>Gets the sort indicator glyph for the Stock column.</summary>
    public string StockSortIndicator => GetSortIndicator(SortStockKey);

    /// <summary>
    /// Initializes a new instance of <see cref="InventoryViewModel"/> and loads initial inventory data.
    /// </summary>
    public InventoryViewModel(
        ICatalogService catalogService,
        IInventoryService inventoryService,
        IInventoryExportService exportService,
        IDialogService dialogService,
        ISettingsService? settingsService = null)
    {
        _catalogService = catalogService;
        _inventoryService = inventoryService;
        _exportService = exportService;
        _dialogService = dialogService;
        _settingsService = settingsService;
        _selectedStatusFilter = StatusFilters[0];

        if (_settingsService != null)
        {
            ApplySettings(_settingsService.GetSettings());
            _settingsService.SettingsChanged += ApplySettings;
        }

        LoadData();
    }

    /// <summary>
    /// Reloads inventory summaries, recomputes KPIs, refreshes filter dropdowns, and updates the current page.
    /// </summary>
    public void LoadData()
    {
        _allItems = _inventoryService.GetProductStockSummary().ToList();

        TotalProductsCount = _allItems.Count;
        LowAndOutOfStockCount = _allItems.Count(item => item.IsLowStock || item.IsOutOfStock);
        ActiveCategoriesCount = _allItems
            .Select(item => item.Category.Trim())
            .Where(category => !string.IsNullOrEmpty(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        EstimatedTotalValue = _allItems.Sum(item => item.SalePrice * item.TotalStock);

        TotalProductCountChanged?.Invoke(TotalProductsCount);

        _isUpdatingFilters = true;
        try
        {
            var previousCategory = SelectedCategoryFilter?.Value;
            CategoryFilters.Clear();
            CategoryFilters.Add(new StringFilterOption(null, "Categoría: Todas"));
            foreach (var category in _allItems
                .Select(item => item.Category.Trim())
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
            {
                CategoryFilters.Add(new StringFilterOption(category, category));
            }

            SelectedCategoryFilter = previousCategory != null
                ? CategoryFilters.FirstOrDefault(c => string.Equals(c.Value, previousCategory, StringComparison.OrdinalIgnoreCase))
                    ?? CategoryFilters[0]
                : CategoryFilters[0];

            var previousSupplierId = SelectedSupplierFilter?.SupplierId;
            SupplierFilters.Clear();
            SupplierFilters.Add(new SupplierFilterOption(null, "Proveedor: Todos"));
            foreach (var supplier in _catalogService.GetAllSuppliers()
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
            {
                SupplierFilters.Add(new SupplierFilterOption(supplier.Id, supplier.Name));
            }

            SelectedSupplierFilter = previousSupplierId.HasValue
                ? SupplierFilters.FirstOrDefault(s => s.SupplierId == previousSupplierId.Value) ?? SupplierFilters[0]
                : SupplierFilters[0];

            SelectedStatusFilter ??= StatusFilters[0];
        }
        finally
        {
            _isUpdatingFilters = false;
        }

        ApplyFiltersAndPagination(resetToFirstPage: false);
    }

    /// <summary>
    /// Clears search and dropdown filters and returns to page 1.
    /// </summary>
    [RelayCommand]
    public void ResetFilters()
    {
        ClearStatus();
        _isUpdatingFilters = true;
        try
        {
            SearchText = string.Empty;
            SelectedCategoryFilter = CategoryFilters.FirstOrDefault();
            SelectedStatusFilter = StatusFilters[0];
            SelectedSupplierFilter = SupplierFilters.FirstOrDefault();
        }
        finally
        {
            _isUpdatingFilters = false;
        }

        ApplyFiltersAndPagination(resetToFirstPage: true);
    }

    /// <summary>
    /// Filters the table to show only low-stock and out-of-stock products.
    /// </summary>
    [RelayCommand]
    public void FilterLowStock()
    {
        ClearStatus();
        _isUpdatingFilters = true;
        try
        {
            SearchText = string.Empty;
            SelectedCategoryFilter = CategoryFilters.FirstOrDefault();
            SelectedSupplierFilter = SupplierFilters.FirstOrDefault();
            SelectedStatusFilter = StatusFilters.First(option => option.Value == StatusAlerts);
        }
        finally
        {
            _isUpdatingFilters = false;
        }

        ApplyFiltersAndPagination(resetToFirstPage: true);
    }

    /// <summary>
    /// Toggles or sets sorting for the specified column key across the entire filtered result set.
    /// </summary>
    [RelayCommand]
    public void SortBy(string columnKey)
    {
        if (string.IsNullOrWhiteSpace(columnKey))
            return;

        if (string.Equals(SortColumn, columnKey, StringComparison.Ordinal))
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortColumn = columnKey;
            SortAscending = true;
        }

        NotifySortIndicators();
        ApplyFiltersAndPagination(resetToFirstPage: true);
    }

    /// <summary>
    /// Moves pagination back one page when possible.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    public void PreviousPage()
    {
        if (!CanGoPrevious)
            return;

        CurrentPage--;
        UpdateCurrentPageSlice();
    }

    /// <summary>
    /// Advances pagination forward one page when possible.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGoNext))]
    public void NextPage()
    {
        if (!CanGoNext)
            return;

        CurrentPage++;
        UpdateCurrentPageSlice();
    }

    /// <summary>
    /// Jumps directly to the specified page number within valid bounds.
    /// </summary>
    [RelayCommand]
    public void GoToPage(int page)
    {
        if (page < 1 || page > TotalPages || page == CurrentPage)
            return;

        CurrentPage = page;
        UpdateCurrentPageSlice();
    }

    /// <summary>
    /// Opens the modal product form to add a new product and optional initial stock.
    /// </summary>
    [RelayCommand]
    public void CreateProduct()
    {
        ClearStatus();
        var defaultMinStock = _settingsService?.GetSettings().DefaultLowStockThreshold ?? 10;
        var formViewModel = new ProductFormViewModel(_catalogService, _inventoryService, defaultMinStock: defaultMinStock);
        if (_dialogService.ShowProductForm(formViewModel))
        {
            LoadData();
            SetSuccessStatus("Producto agregado al inventario.");
        }
    }

    /// <summary>
    /// Opens the modal product form for the row represented by <paramref name="item"/>.
    /// </summary>
    [RelayCommand]
    public void EditProductByItem(ProductStockSummaryDto? item)
    {
        if (item == null)
            return;

        ClearStatus();
        var product = _catalogService.GetAllProducts().FirstOrDefault(p => p.Id == item.ProductId);
        if (product == null)
        {
            SetErrorStatus("No se encontró el producto seleccionado.");
            return;
        }

        var formViewModel = new ProductFormViewModel(_catalogService, _inventoryService, product);
        if (_dialogService.ShowProductForm(formViewModel))
        {
            LoadData();
            SetSuccessStatus("Producto actualizado correctamente.");
        }
    }

    /// <summary>
    /// Requests confirmation and deletes the product represented by <paramref name="item"/> when allowed.
    /// </summary>
    [RelayCommand]
    public void DeleteProductByItem(ProductStockSummaryDto? item)
    {
        if (item == null)
            return;

        ClearStatus();
        if (!_dialogService.Confirm(
            $"Se eliminará \"{item.ProductName}\" ({item.ProductSku}) del inventario.",
            "¿Eliminar producto?"))
        {
            return;
        }

        try
        {
            _catalogService.DeleteProduct(item.ProductId);
            LoadData();
            SetSuccessStatus("Producto eliminado correctamente.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            SetErrorStatus(ex.Message);
        }
    }

    /// <summary>
    /// Exports all currently filtered and sorted inventory items to a user-selected CSV file.
    /// </summary>
    [RelayCommand]
    public void ExportCsv()
    {
        ClearStatus();
        if (_filteredItems.Count == 0)
        {
            SetErrorStatus("No hay productos visibles para exportar.");
            return;
        }

        var targetPath = _dialogService.ShowSaveFileDialog(
            "inventario_productos.csv",
            "Archivo CSV (*.csv)|*.csv");

        if (string.IsNullOrWhiteSpace(targetPath))
            return;

        try
        {
            var csv = _exportService.GenerateInventoryCsv(_filteredItems);
            File.WriteAllText(targetPath, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            SetSuccessStatus("Archivo CSV exportado correctamente.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetErrorStatus($"No se pudo guardar el archivo CSV: {ex.Message}");
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        if (!_isUpdatingFilters)
            ApplyFiltersAndPagination(resetToFirstPage: true);
    }

    partial void OnSelectedCategoryFilterChanged(StringFilterOption? value)
    {
        if (!_isUpdatingFilters)
            ApplyFiltersAndPagination(resetToFirstPage: true);
    }

    partial void OnSelectedStatusFilterChanged(StringFilterOption? value)
    {
        if (!_isUpdatingFilters)
            ApplyFiltersAndPagination(resetToFirstPage: true);
    }

    partial void OnSelectedSupplierFilterChanged(SupplierFilterOption? value)
    {
        if (!_isUpdatingFilters)
            ApplyFiltersAndPagination(resetToFirstPage: true);
    }

    /// <summary>
    /// Filters and sorts the cached inventory collection and updates page bounds.
    /// </summary>
    private void ApplyFiltersAndPagination(bool resetToFirstPage)
    {
        var term = SearchText.Trim();
        var categoryFilter = SelectedCategoryFilter?.Value;
        var statusFilter = SelectedStatusFilter?.Value;
        var supplierFilterId = SelectedSupplierFilter?.SupplierId;

        IEnumerable<ProductStockSummaryDto> query = _allItems.Where(item =>
        {
            var matchesTerm = string.IsNullOrEmpty(term)
                || item.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.ProductSku.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.Category.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.SupplierName.Contains(term, StringComparison.OrdinalIgnoreCase);

            var matchesCategory = string.IsNullOrEmpty(categoryFilter)
                || string.Equals(item.Category, categoryFilter, StringComparison.OrdinalIgnoreCase);

            var matchesSupplier = !supplierFilterId.HasValue
                || item.SupplierId == supplierFilterId.Value;

            var matchesStatus = statusFilter switch
            {
                null => true,
                StatusAlerts => item.IsLowStock || item.IsOutOfStock,
                _ => string.Equals(item.StockStatusLabel, statusFilter, StringComparison.Ordinal)
            };

            return matchesTerm && matchesCategory && matchesSupplier && matchesStatus;
        });

        query = ApplySort(query);
        _filteredItems = query.ToList();
        FilteredItemsCount = _filteredItems.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(FilteredItemsCount / (double)PageSize));

        if (resetToFirstPage || CurrentPage < 1)
            CurrentPage = 1;
        else if (CurrentPage > TotalPages)
            CurrentPage = TotalPages;

        UpdateCurrentPageSlice();
    }

    /// <summary>
    /// Applies the active sort column and direction to the filtered sequence.
    /// </summary>
    private IEnumerable<ProductStockSummaryDto> ApplySort(IEnumerable<ProductStockSummaryDto> query)
    {
        return SortColumn switch
        {
            SortSkuKey => SortAscending
                ? query.OrderBy(i => i.ProductSku, StringComparer.OrdinalIgnoreCase)
                : query.OrderByDescending(i => i.ProductSku, StringComparer.OrdinalIgnoreCase),
            SortNameKey => SortAscending
                ? query.OrderBy(i => i.ProductName, StringComparer.OrdinalIgnoreCase)
                : query.OrderByDescending(i => i.ProductName, StringComparer.OrdinalIgnoreCase),
            SortCategoryKey => SortAscending
                ? query.OrderBy(i => i.Category, StringComparer.OrdinalIgnoreCase)
                : query.OrderByDescending(i => i.Category, StringComparer.OrdinalIgnoreCase),
            SortPurchasePriceKey => SortAscending
                ? query.OrderBy(i => i.PurchasePrice)
                : query.OrderByDescending(i => i.PurchasePrice),
            SortSalePriceKey => SortAscending
                ? query.OrderBy(i => i.SalePrice)
                : query.OrderByDescending(i => i.SalePrice),
            SortStockKey => SortAscending
                ? query.OrderBy(i => i.TotalStock)
                : query.OrderByDescending(i => i.TotalStock),
            _ => query
        };
    }

    /// <summary>
    /// Updates <see cref="PagedItems"/>, <see cref="PageNumbers"/>, and pagination state notifications for <see cref="CurrentPage"/>.
    /// </summary>
    private void UpdateCurrentPageSlice()
    {
        PagedItems.Clear();
        foreach (var item in _filteredItems.Skip((CurrentPage - 1) * PageSize).Take(PageSize))
        {
            PagedItems.Add(item);
        }

        PageNumbers.Clear();
        for (var i = 1; i <= TotalPages; i++)
        {
            PageNumbers.Add(new PageNumberItem(i, i == CurrentPage));
        }

        OnPropertyChanged(nameof(PaginationInfoText));
        OnPropertyChanged(nameof(HasNoResults));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private string GetSortIndicator(string columnKey)
    {
        if (!string.Equals(SortColumn, columnKey, StringComparison.Ordinal))
            return "↕";

        return SortAscending ? "↑" : "↓";
    }

    private void NotifySortIndicators()
    {
        OnPropertyChanged(nameof(SkuSortIndicator));
        OnPropertyChanged(nameof(NameSortIndicator));
        OnPropertyChanged(nameof(CategorySortIndicator));
        OnPropertyChanged(nameof(PurchasePriceSortIndicator));
        OnPropertyChanged(nameof(SalePriceSortIndicator));
        OnPropertyChanged(nameof(StockSortIndicator));
    }

    private void ClearStatus()
    {
        StatusMessage = string.Empty;
        IsErrorMessage = false;
    }

    private void SetSuccessStatus(string message)
    {
        IsErrorMessage = false;
        StatusMessage = message;
    }

    private void SetErrorStatus(string message)
    {
        IsErrorMessage = true;
        StatusMessage = message;
    }

    private void ApplySettings(AppSettings settings)
    {
        CurrencyCode = settings.CurrencyCode;
        CurrencySymbol = settings.CurrencySymbol;
    }
}

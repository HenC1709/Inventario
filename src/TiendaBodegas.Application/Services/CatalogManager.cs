using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Interfaces;

namespace TiendaBodegas.Application.Services;

/// <summary>
/// Application service enforcing validation and referential integrity rules for products and warehouses.
/// </summary>
public class CatalogManager : ICatalogService
{
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IStockMovementRepository _movementRepository;

    /// <summary>
    /// Initializes a new instance of <see cref="CatalogManager"/>.
    /// </summary>
    public CatalogManager(
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IStockMovementRepository movementRepository)
    {
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _movementRepository = movementRepository;
    }

    /// <summary>
    /// Retrieves all persisted products.
    /// </summary>
    public IEnumerable<Product> GetAllProducts() => _productRepository.GetAll();

    /// <summary>
    /// Validates product inputs, checks SKU uniqueness, and creates a new product.
    /// </summary>
    public Product CreateProduct(string name, string sku, decimal price)
    {
        ValidateProductInputs(name, sku, price, excludeProductId: null);

        var product = new Product(name.Trim(), sku.Trim(), price);
        _productRepository.Add(product);
        return product;
    }

    /// <summary>
    /// Validates product inputs, checks SKU uniqueness, and updates the matching product.
    /// </summary>
    public void UpdateProduct(int id, string name, string sku, decimal price)
    {
        var existing = _productRepository.GetById(id)
            ?? throw new ArgumentException("El producto especificado no existe.", nameof(id));

        ValidateProductInputs(name, sku, price, excludeProductId: id);

        existing.Update(name, sku, price);
        _productRepository.Update(existing);
    }

    /// <summary>
    /// Deletes a product only when it has no recorded stock movements.
    /// </summary>
    public void DeleteProduct(int id)
    {
        _ = _productRepository.GetById(id)
            ?? throw new ArgumentException("El producto especificado no existe.", nameof(id));

        if (_movementRepository.GetAll().Any(movement => movement.ProductId == id))
            throw new InvalidOperationException("No se puede eliminar un producto que ya tiene movimientos registrados.");

        _productRepository.Delete(id);
    }

    /// <summary>
    /// Determines whether the provided SKU is free to use (optionally excluding the product being edited).
    /// </summary>
    public bool IsSkuAvailable(string sku, int? excludeProductId = null)
    {
        if (string.IsNullOrWhiteSpace(sku))
            return false;

        var existing = _productRepository.GetBySku(sku.Trim());
        return existing == null || (excludeProductId.HasValue && existing.Id == excludeProductId.Value);
    }

    /// <summary>
    /// Retrieves all persisted warehouses.
    /// </summary>
    public IEnumerable<Warehouse> GetAllWarehouses() => _warehouseRepository.GetAll();

    /// <summary>
    /// Validates warehouse inputs and creates a new warehouse.
    /// </summary>
    public Warehouse CreateWarehouse(string name, string address)
    {
        ValidateWarehouseInputs(name, address);

        var warehouse = new Warehouse(name.Trim(), address.Trim());
        _warehouseRepository.Add(warehouse);
        return warehouse;
    }

    /// <summary>
    /// Validates warehouse inputs and updates the matching warehouse.
    /// </summary>
    public void UpdateWarehouse(int id, string name, string address)
    {
        var existing = _warehouseRepository.GetById(id)
            ?? throw new ArgumentException("La bodega especificada no existe.", nameof(id));

        ValidateWarehouseInputs(name, address);

        existing.Update(name, address);
        _warehouseRepository.Update(existing);
    }

    /// <summary>
    /// Deletes a warehouse only when it has no recorded stock movements.
    /// </summary>
    public void DeleteWarehouse(int id)
    {
        _ = _warehouseRepository.GetById(id)
            ?? throw new ArgumentException("La bodega especificada no existe.", nameof(id));

        if (_movementRepository.GetByWarehouseId(id).Any())
            throw new InvalidOperationException("No se puede eliminar una bodega que ya tiene movimientos registrados.");

        _warehouseRepository.Delete(id);
    }

    /// <summary>
    /// Validates required fields, positive price, and SKU uniqueness for product creation or update.
    /// </summary>
    private void ValidateProductInputs(string name, string sku, decimal price, int? excludeProductId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del producto es obligatorio.", nameof(name));

        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("El SKU del producto es obligatorio.", nameof(sku));

        if (price <= 0)
            throw new ArgumentOutOfRangeException(nameof(price), "El precio debe ser mayor a cero.");

        if (!IsSkuAvailable(sku, excludeProductId))
            throw new InvalidOperationException("Ya existe un producto con el mismo SKU.");
    }

    /// <summary>
    /// Validates required fields for warehouse creation or update.
    /// </summary>
    private static void ValidateWarehouseInputs(string name, string address)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la bodega es obligatorio.", nameof(name));

        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("La dirección de la bodega es obligatoria.", nameof(address));
    }
}

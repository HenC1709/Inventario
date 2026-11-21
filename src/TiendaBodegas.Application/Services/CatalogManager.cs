using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Enums;
using TiendaBodegas.Domain.Interfaces;

namespace TiendaBodegas.Application.Services;

/// <summary>
/// Application service enforcing validation and referential integrity rules for products and suppliers.
/// </summary>
public class CatalogManager : ICatalogService
{
    private readonly IProductRepository _productRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IStockMovementRepository _movementRepository;

    /// <summary>
    /// Initializes a new instance of <see cref="CatalogManager"/>.
    /// </summary>
    public CatalogManager(
        IProductRepository productRepository,
        ISupplierRepository supplierRepository,
        IStockMovementRepository movementRepository)
    {
        _productRepository = productRepository;
        _supplierRepository = supplierRepository;
        _movementRepository = movementRepository;
    }

    /// <summary>
    /// Retrieves all persisted products.
    /// </summary>
    public IEnumerable<Product> GetAllProducts() => _productRepository.GetAll();

    /// <summary>
    /// Validates product inputs, checks SKU uniqueness and supplier existence, and creates a new product.
    /// </summary>
    public Product CreateProduct(
        string name,
        string sku,
        string category,
        int? supplierId,
        decimal purchasePrice,
        decimal salePrice,
        int minStock = 0)
    {
        ValidateProductInputs(name, sku, category, supplierId, purchasePrice, salePrice, minStock, excludeProductId: null);

        var product = new Product(name, sku, category, supplierId, purchasePrice, salePrice, minStock);
        _productRepository.Add(product);
        return product;
    }

    /// <summary>
    /// Validates product inputs, checks SKU uniqueness and supplier existence, and updates the matching product.
    /// </summary>
    public void UpdateProduct(
        int id,
        string name,
        string sku,
        string category,
        int? supplierId,
        decimal purchasePrice,
        decimal salePrice,
        int minStock = 0)
    {
        var existing = _productRepository.GetById(id)
            ?? throw new ArgumentException("El producto especificado no existe.", nameof(id));

        ValidateProductInputs(name, sku, category, supplierId, purchasePrice, salePrice, minStock, excludeProductId: id);

        existing.Update(name, sku, category, supplierId, purchasePrice, salePrice, minStock);
        _productRepository.Update(existing);
    }

    /// <summary>
    /// Deletes a product only when it has no recorded stock movements.
    /// </summary>
    public void DeleteProduct(int id)
    {
        _ = _productRepository.GetById(id)
            ?? throw new ArgumentException("El producto especificado no existe.", nameof(id));

        if (_movementRepository.GetByProductId(id).Any())
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
    /// Retrieves all persisted suppliers.
    /// </summary>
    public IEnumerable<Supplier> GetAllSuppliers() => _supplierRepository.GetAll();

    /// <summary>
    /// Validates supplier inputs, verifies optional code uniqueness, and creates a new supplier.
    /// </summary>
    public Supplier CreateSupplier(
        string name,
        string contact,
        string mainCategory,
        SupplierStatus status = SupplierStatus.Active,
        string code = "")
    {
        ValidateSupplierInputs(name, contact, mainCategory, status, code, excludeSupplierId: null);

        var supplier = new Supplier(name, contact, mainCategory, status, code);
        _supplierRepository.Add(supplier);
        return supplier;
    }

    /// <summary>
    /// Validates supplier inputs, verifies optional code uniqueness, and updates the matching supplier.
    /// </summary>
    public void UpdateSupplier(
        int id,
        string name,
        string contact,
        string mainCategory,
        SupplierStatus status,
        string? code = null)
    {
        var existing = _supplierRepository.GetById(id)
            ?? throw new ArgumentException("El proveedor especificado no existe.", nameof(id));

        ValidateSupplierInputs(name, contact, mainCategory, status, code, excludeSupplierId: id);

        existing.Update(name, contact, mainCategory, status, code);
        _supplierRepository.Update(existing);
    }

    /// <summary>
    /// Deletes a supplier only when it has no linked products.
    /// </summary>
    public void DeleteSupplier(int id)
    {
        _ = _supplierRepository.GetById(id)
            ?? throw new ArgumentException("El proveedor especificado no existe.", nameof(id));

        if (_productRepository.GetBySupplierId(id).Any())
            throw new InvalidOperationException("No se puede eliminar un proveedor que tiene productos asociados.");

        _supplierRepository.Delete(id);
    }

    /// <summary>
    /// Determines whether the provided supplier code is non-empty and free to use (optionally excluding the supplier being edited).
    /// </summary>
    public bool IsSupplierCodeAvailable(string code, int? excludeSupplierId = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var existing = _supplierRepository.GetByCode(code.Trim());
        return existing == null || (excludeSupplierId.HasValue && existing.Id == excludeSupplierId.Value);
    }

    /// <summary>
    /// Validates required fields, pricing bounds, supplier reference, non-negative minimum stock, and SKU uniqueness.
    /// </summary>
    private void ValidateProductInputs(
        string name,
        string sku,
        string category,
        int? supplierId,
        decimal purchasePrice,
        decimal salePrice,
        int minStock,
        int? excludeProductId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del producto es obligatorio.", nameof(name));

        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("El SKU del producto es obligatorio.", nameof(sku));

        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("La categoría del producto es obligatoria.", nameof(category));

        if (purchasePrice < 0)
            throw new ArgumentOutOfRangeException(nameof(purchasePrice), "El precio de compra no puede ser negativo.");

        if (salePrice <= 0)
            throw new ArgumentOutOfRangeException(nameof(salePrice), "El precio de venta debe ser mayor a cero.");

        if (minStock < 0)
            throw new ArgumentOutOfRangeException(nameof(minStock), "El stock mínimo no puede ser negativo.");

        if (supplierId.HasValue && _supplierRepository.GetById(supplierId.Value) == null)
            throw new ArgumentException("El proveedor especificado no existe.", nameof(supplierId));

        if (!IsSkuAvailable(sku, excludeProductId))
            throw new InvalidOperationException("Ya existe un producto con el mismo SKU.");
    }

    /// <summary>
    /// Validates required fields, status enum value, and optional code uniqueness for supplier creation or update.
    /// </summary>
    private void ValidateSupplierInputs(
        string name,
        string contact,
        string mainCategory,
        SupplierStatus status,
        string? code,
        int? excludeSupplierId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("La razón social del proveedor es obligatoria.", nameof(name));

        if (string.IsNullOrWhiteSpace(contact))
            throw new ArgumentException("El contacto del proveedor es obligatorio.", nameof(contact));

        if (string.IsNullOrWhiteSpace(mainCategory))
            throw new ArgumentException("La categoría principal del proveedor es obligatoria.", nameof(mainCategory));

        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status), "El estado del proveedor no es válido.");

        if (!string.IsNullOrWhiteSpace(code) && !IsSupplierCodeAvailable(code, excludeSupplierId))
            throw new InvalidOperationException("Ya existe un proveedor con el mismo código.");
    }
}

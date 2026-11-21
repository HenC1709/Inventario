using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.Application.Interfaces;

/// <summary>
/// Application service contract for CRUD management of products and suppliers.
/// </summary>
public interface ICatalogService
{
    /// <summary>
    /// Retrieves all products in the catalog.
    /// </summary>
    IEnumerable<Product> GetAllProducts();

    /// <summary>
    /// Validates and creates a new product with a unique SKU.
    /// </summary>
    Product CreateProduct(
        string name,
        string sku,
        string category,
        int? supplierId,
        decimal purchasePrice,
        decimal salePrice,
        int minStock = 0);

    /// <summary>
    /// Validates and updates an existing product's fields.
    /// </summary>
    void UpdateProduct(
        int id,
        string name,
        string sku,
        string category,
        int? supplierId,
        decimal purchasePrice,
        decimal salePrice,
        int minStock = 0);

    /// <summary>
    /// Deletes a product if it has no associated stock movements.
    /// </summary>
    void DeleteProduct(int id);

    /// <summary>
    /// Checks whether a SKU is available (ignoring the given product id during edits).
    /// </summary>
    bool IsSkuAvailable(string sku, int? excludeProductId = null);

    /// <summary>
    /// Retrieves all suppliers in the catalog.
    /// </summary>
    IEnumerable<Supplier> GetAllSuppliers();

    /// <summary>
    /// Validates and creates a new supplier.
    /// </summary>
    Supplier CreateSupplier(
        string name,
        string contact,
        string mainCategory,
        SupplierStatus status = SupplierStatus.Active,
        string code = "");

    /// <summary>
    /// Validates and updates an existing supplier's fields.
    /// </summary>
    void UpdateSupplier(
        int id,
        string name,
        string contact,
        string mainCategory,
        SupplierStatus status,
        string? code = null);

    /// <summary>
    /// Deletes a supplier if it has no linked products.
    /// </summary>
    void DeleteSupplier(int id);

    /// <summary>
    /// Checks whether a supplier code is valid and available (ignoring the given supplier id during edits).
    /// </summary>
    bool IsSupplierCodeAvailable(string code, int? excludeSupplierId = null);
}

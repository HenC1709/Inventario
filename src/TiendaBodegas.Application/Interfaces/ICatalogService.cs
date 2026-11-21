using TiendaBodegas.Domain.Entities;

namespace TiendaBodegas.Application.Interfaces;

/// <summary>
/// Application service contract for CRUD management of products and warehouses.
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
    Product CreateProduct(string name, string sku, decimal price, int minStock = 0);

    /// <summary>
    /// Validates and updates an existing product's fields.
    /// </summary>
    void UpdateProduct(int id, string name, string sku, decimal price, int minStock = 0);

    /// <summary>
    /// Deletes a product if it has no associated stock movements.
    /// </summary>
    void DeleteProduct(int id);

    /// <summary>
    /// Checks whether a SKU is available (ignoring the given product id during edits).
    /// </summary>
    bool IsSkuAvailable(string sku, int? excludeProductId = null);

    /// <summary>
    /// Retrieves all warehouses.
    /// </summary>
    IEnumerable<Warehouse> GetAllWarehouses();

    /// <summary>
    /// Validates and creates a new warehouse.
    /// </summary>
    Warehouse CreateWarehouse(string name, string address);

    /// <summary>
    /// Validates and updates an existing warehouse's fields.
    /// </summary>
    void UpdateWarehouse(int id, string name, string address);

    /// <summary>
    /// Deletes a warehouse if it has no associated stock movements.
    /// </summary>
    void DeleteWarehouse(int id);
}

using TiendaBodegas.Domain.Entities;

namespace TiendaBodegas.Domain.Interfaces;

/// <summary>
/// Repository contract for managing <see cref="Product"/> persistence and lookups.
/// </summary>
public interface IProductRepository : IRepository<Product>
{
    /// <summary>
    /// Finds a product by its unique SKU code.
    /// </summary>
    Product? GetBySku(string sku);

    /// <summary>
    /// Finds a product by its exact name.
    /// </summary>
    Product? GetByName(string name);

    /// <summary>
    /// Updates an existing product's persisted data.
    /// </summary>
    void Update(Product product);

    /// <summary>
    /// Deletes the product with the specified identifier from persistence.
    /// </summary>
    void Delete(int id);
}

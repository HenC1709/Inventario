using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Interfaces;
using TiendaBodegas.Infrastructure.Persistence.Json;

namespace TiendaBodegas.Infrastructure.Persistence.Repositories;

/// <summary>
/// JSON-backed repository implementation for <see cref="Product"/> entities.
/// </summary>
public class ProductRepository : IProductRepository
{
    private readonly JsonDataContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="ProductRepository"/>.
    /// </summary>
    public ProductRepository(JsonDataContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves all persisted products.
    /// </summary>
    public IEnumerable<Product> GetAll()
    {
        return _context.Products;
    }

    /// <summary>
    /// Finds a product by its unique identifier.
    /// </summary>
    public Product? GetById(int id)
    {
        return _context.Products.FirstOrDefault(product => product.Id == id);
    }

    /// <summary>
    /// Finds a product by its SKU code (case-insensitive).
    /// </summary>
    public Product? GetBySku(string sku)
    {
        return _context.Products.FirstOrDefault(product =>
            string.Equals(product.SKU, sku, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Finds a product by its exact name.
    /// </summary>
    public Product? GetByName(string name)
    {
        return _context.Products.FirstOrDefault(product => product.Name == name);
    }

    /// <summary>
    /// Retrieves all products linked to the specified supplier identifier.
    /// </summary>
    public IEnumerable<Product> GetBySupplierId(int supplierId)
    {
        return _context.Products.Where(product => product.SupplierId == supplierId);
    }

    /// <summary>
    /// Assigns the next sequential identifier, adds the product, and persists changes to JSON.
    /// </summary>
    public void Add(Product product)
    {
        var nextId = _context.Products.Any()
            ? _context.Products.Max(item => item.Id) + 1
            : 1;
        product.AssignId(nextId);
        _context.Products.Add(product);
        _context.SaveJson();
    }

    /// <summary>
    /// Replaces an existing product with the updated instance and saves changes to JSON.
    /// </summary>
    public void Update(Product product)
    {
        var index = _context.Products.FindIndex(item => item.Id == product.Id);
        if (index >= 0)
        {
            _context.Products[index] = product;
            _context.SaveJson();
        }
    }

    /// <summary>
    /// Removes the product with the specified identifier and persists changes to JSON.
    /// </summary>
    public void Delete(int id)
    {
        var existingProduct = _context.Products.FirstOrDefault(item => item.Id == id);
        if (existingProduct != null)
        {
            _context.Products.Remove(existingProduct);
            _context.SaveJson();
        }
    }
}

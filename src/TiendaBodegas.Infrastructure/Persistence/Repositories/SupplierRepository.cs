using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Interfaces;
using TiendaBodegas.Infrastructure.Persistence.Json;

namespace TiendaBodegas.Infrastructure.Persistence.Repositories;

/// <summary>
/// JSON-backed repository implementation for <see cref="Supplier"/> entities.
/// </summary>
public class SupplierRepository : ISupplierRepository
{
    private readonly JsonDataContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="SupplierRepository"/>.
    /// </summary>
    public SupplierRepository(JsonDataContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves all persisted suppliers.
    /// </summary>
    public IEnumerable<Supplier> GetAll()
    {
        return _context.Suppliers;
    }

    /// <summary>
    /// Finds a supplier by its unique identifier.
    /// </summary>
    public Supplier? GetById(int id)
    {
        return _context.Suppliers.FirstOrDefault(supplier => supplier.Id == id);
    }

    /// <summary>
    /// Finds a supplier by its unique code (case-insensitive).
    /// </summary>
    public Supplier? GetByCode(string code)
    {
        return _context.Suppliers.FirstOrDefault(supplier =>
            string.Equals(supplier.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Finds a supplier by its business name (case-insensitive).
    /// </summary>
    public Supplier? GetByName(string name)
    {
        return _context.Suppliers.FirstOrDefault(supplier =>
            string.Equals(supplier.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Assigns the next sequential identifier, generates a default code if needed, adds the supplier, and persists to JSON.
    /// </summary>
    public void Add(Supplier supplier)
    {
        var nextId = _context.Suppliers.Any()
            ? _context.Suppliers.Max(item => item.Id) + 1
            : 1;
        supplier.AssignId(nextId);
        _context.Suppliers.Add(supplier);
        _context.SaveJson();
    }

    /// <summary>
    /// Replaces an existing supplier in place and saves changes to JSON.
    /// </summary>
    public void Update(Supplier supplier)
    {
        var index = _context.Suppliers.FindIndex(item => item.Id == supplier.Id);
        if (index >= 0)
        {
            _context.Suppliers[index] = supplier;
            _context.SaveJson();
        }
    }

    /// <summary>
    /// Removes the supplier with the specified identifier and persists changes to JSON.
    /// </summary>
    public void Delete(int id)
    {
        var existingSupplier = _context.Suppliers.FirstOrDefault(item => item.Id == id);
        if (existingSupplier != null)
        {
            _context.Suppliers.Remove(existingSupplier);
            _context.SaveJson();
        }
    }
}

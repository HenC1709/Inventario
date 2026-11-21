using TiendaBodegas.Domain.Entities;

namespace TiendaBodegas.Domain.Interfaces;

/// <summary>
/// Repository contract for managing <see cref="Supplier"/> persistence and lookups.
/// </summary>
public interface ISupplierRepository : IRepository<Supplier>
{
    /// <summary>
    /// Finds a supplier by its unique code (case-insensitive).
    /// </summary>
    Supplier? GetByCode(string code);

    /// <summary>
    /// Finds a supplier by its business name (case-insensitive).
    /// </summary>
    Supplier? GetByName(string name);

    /// <summary>
    /// Updates an existing supplier's persisted data.
    /// </summary>
    void Update(Supplier supplier);

    /// <summary>
    /// Deletes the supplier with the specified identifier from persistence.
    /// </summary>
    void Delete(int id);
}

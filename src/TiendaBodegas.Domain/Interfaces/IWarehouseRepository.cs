using TiendaBodegas.Domain.Entities;

namespace TiendaBodegas.Domain.Interfaces;
/// <summary>
/// Repository contract for managing <see cref="Warehouse"/> persistence and lookups.
/// </summary>
public interface IWarehouseRepository : IRepository<Warehouse>
{
    /// <summary>
    /// Finds a warehouse by its exact name.
    /// </summary>
    Warehouse? GetByName(string name);

    /// <summary>
    /// Finds a warehouse by its exact address.
    /// </summary>
    Warehouse? GetByAddress(string address);

    /// <summary>
    /// Updates an existing warehouse's persisted data.
    /// </summary>
    void Update(Warehouse warehouse);
}

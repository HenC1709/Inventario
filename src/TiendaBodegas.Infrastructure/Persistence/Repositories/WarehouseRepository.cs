using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Interfaces;
using TiendaBodegas.Infrastructure.Persistence.Json;


namespace TiendaBodegas.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// JSON-backed repository implementation for <see cref="Warehouse"/> entities.
    /// </summary>
    public class WarehouseRepository : IWarehouseRepository
    {
        private readonly JsonDataContext _context;

        /// <summary>
        /// Initializes a new instance of <see cref="WarehouseRepository"/>.
        /// </summary>
        public WarehouseRepository(JsonDataContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves all persisted warehouses.
        /// </summary>
        public IEnumerable<Warehouse> GetAll()
        {
            return _context.Warehouses;
        }

        /// <summary>
        /// Finds the first warehouse matching the given name.
        /// </summary>
        public Warehouse? GetByName(string name)
        {
            return _context.Warehouses.FirstOrDefault(warehouse => warehouse.Name == name);
        }

        /// <summary>
        /// Finds the first warehouse matching the given address.
        /// </summary>
        public Warehouse? GetByAddress(string address)
        {
            return _context.Warehouses.FirstOrDefault(warehouse => warehouse.Address == address);
        }

        /// <summary>
        /// Replaces an existing warehouse with the updated instance and saves changes to JSON.
        /// </summary>
        public void Update(Warehouse warehouse)
        {
            var existingWarehouse = _context.Warehouses.FirstOrDefault(item => item.Id == warehouse.Id);
            if (existingWarehouse != null)
            {
                _context.Warehouses.Remove(existingWarehouse);
                _context.Warehouses.Add(warehouse);
                _context.SaveJson();
            }
        }

        /// <summary>
        /// Assigns the next sequential identifier, adds the warehouse, and persists changes to JSON.
        /// </summary>
        public void Add(Warehouse warehouse)
        {
            var nextId = _context.Warehouses.Any() ?
                _context.Warehouses.Max(item => item.Id) + 1 : 1;
            warehouse.AssignId(nextId);
            _context.Warehouses.Add(warehouse);
            _context.SaveJson();
        }

        /// <summary>
        /// Finds a warehouse by its unique identifier.
        /// </summary>
        public Warehouse? GetById(int id)
        {
            return _context.Warehouses.FirstOrDefault(warehouse => warehouse.Id == id);
        }
    }
}

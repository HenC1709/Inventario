using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Interfaces;
using TiendaBodegas.Infrastructure.Persistence.Json;


namespace TiendaBodegas.Infrastructure.Persistence.Repositories
{
    public class WarehouseRepository : IWarehouseRepository
     {
      private readonly JsonDataContext _context;

      public WarehouseRepository(JsonDataContext context)
        {
            _context = context;
        }

        public IEnumerable<Warehouse> GetAll()
        {
            return _context.Warehouses;
        }
        public Warehouse? GetByName(string name)
        {
            return _context.Warehouses.FirstOrDefault(warehouse => warehouse.Name == name);
        }
        public Warehouse? GetByAddress(string address)
        {
            return _context.Warehouses.FirstOrDefault(warehouse => warehouse.Address == address);
        }
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
        public void Add(Warehouse warehouse)
        {
            var nextId = _context.Warehouses.Any() ?
            _context.Warehouses.Max(item => item.Id) + 1 : 1;
            warehouse.AssignId(nextId);
            _context.Warehouses.Add(warehouse);
            _context.SaveJson();
        }
        public Warehouse? GetById(int id)
        {
            return _context.Warehouses.FirstOrDefault(warehouse => warehouse.Id == id);
        }
    }
}

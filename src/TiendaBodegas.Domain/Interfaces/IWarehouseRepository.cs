using TiendaBodegas.Domain.Entities;

namespace TiendaBodegas.Domain.Interfaces;
public interface IWarehouseRepository : IRepository<Warehouse>
{
    Warehouse? GetByName(string name);
    Warehouse? GetByAddress(string address);
    void Update(Warehouse warehouse);
    
}

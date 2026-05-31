using TiendaBodegas.Domain.Entities;

namespace TiendaBodegas.Domain.Interfaces;
public interface IBodegaRepository : IRepository<Bodega>
{
    Bodega? GetByName(string name);
    Bodega? GetByAddress(string address);
    void Update(Bodega bodega);
    
}
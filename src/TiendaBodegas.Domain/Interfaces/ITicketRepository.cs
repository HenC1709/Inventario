using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.Domain.Interfaces;
public interface ITicketRepository : IRepository<Ticket>
{
    IEnumerable<Ticket> GetByBodegaId(int bodegaId);
    IEnumerable<Ticket> GetByTipoMovimiento(TipoMovimiento tipoMovimiento);
    new void Add(Ticket ticket);
}

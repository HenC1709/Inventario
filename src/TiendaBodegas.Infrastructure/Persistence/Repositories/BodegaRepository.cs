using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Interfaces;
using TiendaBodegas.Infrastructure.Persistence.Json;


namespace TiendaBodegas.Infrastructure.Persistence.Repositories
{
    public class BodegaRepository : IBodegaRepository
     {
      private readonly JsonDbContext _context;

      public BodegaRepository(JsonDbContext context)
        {
            _context = context;
        }

        public IEnumerable<Bodega> GetAll()
        {
            return _context.Bodegas;
        }
        public Bodega? GetByName(string name)
        {
            return _context.Bodegas.FirstOrDefault(b => b.Nombre == name);
        }
        public Bodega? GetByAddress(string address)
        {
            return _context.Bodegas.FirstOrDefault(b => b.Direccion == address);
        }
        public void Update(Bodega bodega)
        {
            var existingBodega = _context.Bodegas.FirstOrDefault(b => b.Id == bodega.Id);
            if (existingBodega != null)
            {
                _context.Bodegas.Remove(existingBodega);
                _context.Bodegas.Add(bodega);
                _context.GuardarJson();
            }
        }
        public void Add(Bodega bodega)
        {
            var nuevoId = _context.Bodegas.Any() ?
            _context.Bodegas.Max(b => b.Id) + 1 : 1;
            bodega.AsignarId(nuevoId);
            _context.Bodegas.Add(bodega);
            _context.GuardarJson();
        }
        public Bodega? GetById(int id)
        {
            return _context.Bodegas.FirstOrDefault(b => b.Id == id);
        }
    }
}
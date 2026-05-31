using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Interfaces;
using TiendaBodegas.Domain.Enums;


namespace TiendaBodegas.Application.Services
{
    public class InventarioManager : IInventarioService
    {
        private readonly ITicketRepository _ticketRepository;
        private readonly IBodegaRepository _bodegaRepository;
    
        public InventarioManager(ITicketRepository ticketRepository, IBodegaRepository bodegaRepository)
        {
            _ticketRepository = ticketRepository;
            _bodegaRepository = bodegaRepository;
        }
        public void RealizarTraslado(int productoId, int origenId, int destinoId, int cantidad, string obs)
        {
             // Validación barata primero (sin ir a la DB)
            if (origenId == destinoId)
            throw new ArgumentException("La bodega de origen y destino no pueden ser iguales");


            // 1 validar: si origen y destino son diferentes?
             var bodegaOrigen = _bodegaRepository.GetById(origenId);
             var bodegaDestino = _bodegaRepository.GetById(destinoId); // FIX: PascalCase → camelCase

            if (bodegaOrigen == null || bodegaDestino == null)
            throw new ArgumentException("Una o ambas Bodegas no existen en el sistema");

            // FIX: ahora sí se usa stockOrigen para validar
             var stockOrigen = _ticketRepository.GetByBodegaId(origenId)
                .Where(t => t.ProductoId == productoId)
                .Sum(t => t.Tipo == TipoMovimiento.Entrada ? t.Cantidad :
                t.Tipo == TipoMovimiento.Salida ? -t.Cantidad:
                t.BodegaDestinoId == origenId ? t.Cantidad : -t.Cantidad); // traslado

                if (stockOrigen < cantidad)
                throw new InvalidOperationException(
                    $"Stock insuficiente en bodega origen. Disponible: {stockOrigen}, solicitado: {cantidad}");
                  
            // 3: Ejecucion!
            var nuevoTicket = new Ticket(
                productoId, cantidad,
                TipoMovimiento.Traslado,
                origenId, destinoId, obs
            );
            _ticketRepository.Add(nuevoTicket);
        }
        public void RegistrarEntrada(int productoId, int bodegaId, int cantidad, string obs)
        {
            var nuevoTicket = new Ticket(
                productoId, cantidad,
                TipoMovimiento.Entrada,
                null, bodegaId, obs
            );
            _ticketRepository.Add(nuevoTicket);
        }
        public void RegistrarSalida(int productoId, int bodegaId, int cantidad, string obs)
        {
            var nuevoTicket = new Ticket(
                productoId, cantidad,
                TipoMovimiento.Salida,
                bodegaId, null, obs
            );
            _ticketRepository.Add(nuevoTicket);
        }

        public IEnumerable<Inventario> ObtenerStockPorBodega(int bodegaId)
        {
            var tickets = _ticketRepository.GetByBodegaId(bodegaId);
           return tickets.GroupBy(t => t.ProductoId)
           .Select(g => new Inventario(
            g.Key, bodegaId, 
            // FIX: traslados ya no siempre restan — depende de si esta bodega
            // es origen (sale stock) o destino (entra stock)
            g.Sum(t => t.Tipo == TipoMovimiento.Entrada ? t.Cantidad :
            t.Tipo == TipoMovimiento.Salida ? -t.Cantidad :
            t.BodegaDestinoId == bodegaId ? t.Cantidad : -t.Cantidad)
           ));
        }

        public int ObtenerStockPorTotalProducto(int productoId)
        {
            // FIX: los traslados no afectan el stock TOTAL del sistema
            // (lo que sale de una bodega entra a otra, se cancelan).
            // Solo importan entradas y salidas reales.
           var entradas = _ticketRepository.GetByTipoMovimiento(TipoMovimiento.Entrada)
           .Where(t => t.ProductoId == productoId)
           .Sum(t => t.Cantidad);

           var salidas = _ticketRepository.GetByTipoMovimiento(TipoMovimiento.Salida)
           .Where(t => t.ProductoId == productoId)
           .Sum(t => t.Cantidad);
           
           return entradas - salidas;
        }
    }
}
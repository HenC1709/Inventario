using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.Domain.Entities
{
    public class Ticket
    {
        public int Id {get; private set; }
        public DateTime Fecha { get; private set; }
        public int ProductoId { get; private set; }
        public int Cantidad { get; private set; }
        public TipoMovimiento Tipo { get; private set; }

        public int? BodegaOrigenId { get; private set; }
        public int? BodegaDestinoId { get; private set; }

        public string Observaciones { get; private set; } = string.Empty;
        public Ticket() { }

        public Ticket(int productoId, int cantidad, TipoMovimiento tipo, int? origenId, int? destinoId, string obs )
        {
            Fecha = DateTime.Now;
            ProductoId = productoId;
            Cantidad = cantidad;
            Tipo = tipo;
            BodegaOrigenId = origenId;
            BodegaDestinoId = destinoId;
            Observaciones = obs;

        }
 
    }
}
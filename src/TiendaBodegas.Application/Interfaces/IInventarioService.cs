using TiendaBodegas.Domain.Entities;

namespace TiendaBodegas.Application.Interfaces
{
public interface IInventarioService
    {
        void RegistrarEntrada(int productoId, int bodegaId, int cantidad, string obs);
        void RegistrarSalida(int productoId, int bodegaId, int cantidad, string obs);
        void RealizarTraslado(int productoId, int origenId, int destinoId, int cantidad, string obs);

        IEnumerable<Inventario> ObtenerStockPorBodega(int bodegaId);
        int ObtenerStockPorTotalProducto(int productoId);
    }

}
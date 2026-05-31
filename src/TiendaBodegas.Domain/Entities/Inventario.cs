namespace TiendaBodegas.Domain.Entities
{
    public class Inventario
    {
        public int Id { get; private set; }
        public int ProductoId { get; private set; }
        public int BodegaId { get; private set; }
        public int Cantidad { get; private set; }
        public Producto Producto { get; private set; } = null!;
        public Bodega Bodega { get; private set; } = null!;

        public Inventario(int productoId, int bodegaId, int cantidad)
        {
            ProductoId = productoId;
            BodegaId = bodegaId;
            Cantidad = cantidad;
        }
    }
}

namespace TiendaBodegas.Domain.Entities;

public class Producto
{
    public int Id { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string SKU { get; private set; } = string.Empty; 
    public decimal Precio { get; private set; }
}
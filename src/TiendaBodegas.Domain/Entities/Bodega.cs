namespace TiendaBodegas.Domain.Entities;

public class Bodega
{
    public int Id { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Direccion { get; private set; } = string.Empty;
}
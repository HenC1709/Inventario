namespace TiendaBodegas.Domain.Entities;

public class Bodega
{
    public int Id { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Direccion { get; private set; } = string.Empty;

     public Bodega() { }

    public Bodega(string nombre, string direccion)
    {
        Nombre = nombre;
        Direccion = direccion;
    }

    public void AsignarId(int id)
    {
        if (Id == 0) Id = id;
    }
}
using System.Text.Json.Serialization;

namespace TiendaBodegas.Domain.Entities;

public class Warehouse
{
    public int Id { get; private set; }
    [JsonPropertyName("Nombre")]
    public string Name { get; private set; } = string.Empty;
    [JsonPropertyName("Direccion")]
    public string Address { get; private set; } = string.Empty;

     public Warehouse() { }

    public Warehouse(string name, string address)
    {
        Name = name;
        Address = address;
    }

    public void AssignId(int id)
    {
        if (Id == 0) Id = id;
    }
}

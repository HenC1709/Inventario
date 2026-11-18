using System.Text.Json.Serialization;

namespace TiendaBodegas.Domain.Entities;

public class Warehouse
{
    [JsonInclude]
    public int Id { get; private set; }
    [JsonInclude]
    [JsonPropertyName("Nombre")]
    public string Name { get; private set; } = string.Empty;
    [JsonInclude]
    [JsonPropertyName("Direccion")]
    public string Address { get; private set; } = string.Empty;

    /// <summary>
    /// Parameterless constructor required for JSON deserialization.
    /// </summary>
    public Warehouse() { }

    /// <summary>
    /// Initializes a new warehouse with its name and address.
    /// </summary>
    public Warehouse(string name, string address)
    {
        Name = name;
        Address = address;
    }

    /// <summary>
    /// Assigns a unique identifier to the warehouse if it has not been assigned yet.
    /// </summary>
    public void AssignId(int id)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), "Id must be greater than zero.");

        if (Id == 0) Id = id;
    }

    /// <summary>
    /// Updates the editable warehouse fields while preserving its identity.
    /// </summary>
    public void Update(string name, string address)
    {
        Name = name.Trim();
        Address = address.Trim();
    }
}

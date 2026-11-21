using System.Text.Json.Serialization;

namespace TiendaBodegas.Domain.Entities;

public class Product
{
    [JsonInclude]
    public int Id { get; private set; }
    [JsonInclude]
    [JsonPropertyName("Nombre")]
    public string Name { get; private set; } = string.Empty;
    [JsonInclude]
    public string SKU { get; private set; } = string.Empty;
    [JsonInclude]
    [JsonPropertyName("Precio")]
    public decimal Price { get; private set; }
    [JsonInclude]
    [JsonPropertyName("StockMinimo")]
    public int MinStock { get; private set; }

    /// <summary>
    /// Parameterless constructor required for JSON deserialization.
    /// </summary>
    public Product() { }

    /// <summary>
    /// Initializes a new product with its name, SKU, unit price, and optional minimum stock threshold.
    /// </summary>
    public Product(string name, string sku, decimal price, int minStock = 0)
    {
        Name = name;
        SKU = sku;
        Price = price;
        MinStock = minStock;
    }

    /// <summary>
    /// Assigns a unique identifier to the product if it has not been assigned yet.
    /// </summary>
    public void AssignId(int id)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), "Id must be greater than zero.");

        if (Id == 0) Id = id;
    }

    /// <summary>
    /// Updates the editable product fields while preserving its identity.
    /// </summary>
    public void Update(string name, string sku, decimal price, int minStock = 0)
    {
        Name = name.Trim();
        SKU = sku.Trim();
        Price = price;
        MinStock = minStock;
    }
}

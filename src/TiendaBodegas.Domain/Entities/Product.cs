using System.Text.Json.Serialization;

namespace TiendaBodegas.Domain.Entities;

public class Product
{
    public int Id { get; private set; }
    [JsonPropertyName("Nombre")]
    public string Name { get; private set; } = string.Empty;
    public string SKU { get; private set; } = string.Empty; 
    [JsonPropertyName("Precio")]
    public decimal Price { get; private set; }

    public Product() {}

    public Product(string name, string sku, decimal price)
    {
        Name = name;
        SKU = sku;
        Price = price;
    } 
     
}

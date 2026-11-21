using System.ComponentModel;
using System.Text.Json.Serialization;

namespace TiendaBodegas.Domain.Entities;

/// <summary>
/// Represents a catalog product with SKU, category, supplier reference, purchase/sale pricing, and minimum stock threshold.
/// </summary>
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
    [JsonPropertyName("Categoria")]
    public string Category { get; private set; } = string.Empty;

    [JsonInclude]
    [JsonPropertyName("ProveedorId")]
    public int? SupplierId { get; private set; }

    [JsonInclude]
    [JsonPropertyName("PrecioCompra")]
    public decimal PurchasePrice { get; private set; }

    [JsonInclude]
    [JsonPropertyName("PrecioVenta")]
    public decimal SalePrice { get; private set; }

    [JsonInclude]
    [JsonPropertyName("StockMinimo")]
    public int MinStock { get; private set; }

    /// <summary>
    /// Backward-compatibility property for deserializing legacy JSON records containing the <c>Precio</c> field.
    /// </summary>
    [JsonInclude]
    [JsonPropertyName("Precio")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public decimal? LegacyPrice
    {
        get => null;
        private set
        {
            if (value.HasValue && SalePrice == 0m)
                SalePrice = value.Value;
        }
    }

    /// <summary>
    /// Parameterless constructor required for JSON deserialization.
    /// </summary>
    public Product() { }

    /// <summary>
    /// Initializes a new product with full catalog, supplier, pricing, and minimum stock properties.
    /// </summary>
    public Product(
        string name,
        string sku,
        string category,
        int? supplierId,
        decimal purchasePrice,
        decimal salePrice,
        int minStock = 0)
    {
        Name = name.Trim();
        SKU = sku.Trim();
        Category = category.Trim();
        SupplierId = supplierId;
        PurchasePrice = purchasePrice;
        SalePrice = salePrice;
        MinStock = minStock;
    }

    /// <summary>
    /// Assigns a unique identifier to the product if it has not been assigned yet.
    /// </summary>
    public void AssignId(int id)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), "Id must be greater than zero.");

        if (Id == 0)
            Id = id;
    }

    /// <summary>
    /// Updates the editable product fields while preserving its identity.
    /// </summary>
    public void Update(
        string name,
        string sku,
        string category,
        int? supplierId,
        decimal purchasePrice,
        decimal salePrice,
        int minStock = 0)
    {
        Name = name.Trim();
        SKU = sku.Trim();
        Category = category.Trim();
        SupplierId = supplierId;
        PurchasePrice = purchasePrice;
        SalePrice = salePrice;
        MinStock = minStock;
    }

    /// <summary>
    /// Converts <see cref="PurchasePrice"/> and <see cref="SalePrice"/> by the specified positive exchange rate.
    /// </summary>
    public void ConvertPrices(decimal exchangeRate, int decimalPlaces = 2)
    {
        if (exchangeRate <= 0m)
            throw new ArgumentOutOfRangeException(nameof(exchangeRate), "La tasa de cambio debe ser mayor a cero.");

        if (decimalPlaces is < 0 or > 4)
            throw new ArgumentOutOfRangeException(nameof(decimalPlaces), "Los decimales deben estar entre 0 y 4.");

        PurchasePrice = Math.Round(PurchasePrice * exchangeRate, decimalPlaces, MidpointRounding.AwayFromZero);
        var convertedSalePrice = Math.Round(SalePrice * exchangeRate, decimalPlaces, MidpointRounding.AwayFromZero);
        var minPositiveSalePrice = decimalPlaces == 0 ? 1m : (decimal)Math.Pow(10, -decimalPlaces);
        SalePrice = SalePrice > 0m && convertedSalePrice == 0m ? minPositiveSalePrice : convertedSalePrice;
    }
}

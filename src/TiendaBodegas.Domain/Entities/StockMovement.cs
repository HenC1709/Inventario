using System.Text.Json.Serialization;
using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.Domain.Entities;

/// <summary>
/// Represents a single-warehouse stock entry or exit movement for a product.
/// </summary>
public class StockMovement
{
    [JsonInclude]
    public int Id { get; private set; }

    [JsonInclude]
    [JsonPropertyName("Fecha")]
    public DateTime Date { get; private set; }

    [JsonInclude]
    [JsonPropertyName("ProductoId")]
    public int ProductId { get; private set; }

    [JsonInclude]
    [JsonPropertyName("Cantidad")]
    public int Quantity { get; private set; }

    [JsonInclude]
    [JsonPropertyName("Tipo")]
    public MovementType MovementType { get; private set; }

    [JsonInclude]
    [JsonPropertyName("Observaciones")]
    public string Notes { get; private set; } = string.Empty;

    /// <summary>
    /// Parameterless constructor required for JSON deserialization.
    /// </summary>
    public StockMovement() { }

    /// <summary>
    /// Initializes a new stock movement (entry or exit) with the current timestamp.
    /// </summary>
    public StockMovement(int productId, int quantity, MovementType movementType, string notes = "")
    {
        Date = DateTime.Now;
        ProductId = productId;
        Quantity = quantity;
        MovementType = movementType;
        Notes = notes ?? string.Empty;
    }

    /// <summary>
    /// Assigns a unique identifier to the stock movement if it has not been assigned yet.
    /// </summary>
    public void AssignId(int id)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), "Id must be greater than zero.");

        if (Id == 0)
            Id = id;
    }
}

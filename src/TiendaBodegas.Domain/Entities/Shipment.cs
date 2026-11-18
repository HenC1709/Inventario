using System.Text.Json.Serialization;
using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.Domain.Entities;

public class Shipment
{
    [JsonInclude]
    public int Id { get; private set; }
    [JsonInclude]
    [JsonPropertyName("TicketId")]
    public int MovementId { get; private set; }
    [JsonInclude]
    [JsonPropertyName("FechaCreacion")]
    public DateTime CreatedAt { get; private set; }
    [JsonInclude]
    [JsonPropertyName("FechaEntregaEstimada")]
    public DateTime? EstimatedDeliveryAt { get; private set; }
    [JsonInclude]
    [JsonPropertyName("Estado")]
    public ShipmentStatus Status { get; private set; }
    [JsonInclude]
    [JsonPropertyName("DireccionDestino")]
    public string DestinationAddress { get; private set; } = string.Empty;
    [JsonInclude]
    [JsonPropertyName("Transportadora")]
    public string Carrier { get; private set; } = string.Empty;
    [JsonInclude]
    [JsonPropertyName("GuiaSeguimiento")]
    public string TrackingNumber { get; private set; } = string.Empty;

    [JsonIgnore]
    public StockMovement StockMovement { get; private set; } = null!;

    /// <summary>
    /// Parameterless constructor required for JSON deserialization.
    /// </summary>
    public Shipment() { }

    /// <summary>
    /// Initializes a new shipment in Pending status for the given stock movement.
    /// </summary>
    public Shipment(int movementId, string address, string carrier)
    {
        MovementId = movementId;
        DestinationAddress = address;
        Carrier = carrier;
        CreatedAt = DateTime.Now;
        Status = ShipmentStatus.Pending;
    }

    /// <summary>
    /// Assigns a unique identifier to the shipment if it has not been assigned yet.
    /// </summary>
    public void AssignId(int id)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), "Id must be greater than zero.");

        if (Id == 0) Id = id;
    }

    /// <summary>
    /// Updates the current delivery status of the shipment.
    /// </summary>
    public void UpdateStatus(ShipmentStatus newStatus)
    {
        Status = newStatus;
    }
}

using System.Text.Json.Serialization;
using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.Domain.Entities;

public class Shipment
{
    public int Id { get; private set; }
    [JsonPropertyName("TicketId")]
    public int MovementId { get; private set; }
    [JsonPropertyName("FechaCreacion")]
    public DateTime CreatedAt { get; private set; }
    [JsonPropertyName("FechaEntregaEstimada")]
    public DateTime? EstimatedDeliveryAt { get; private set; }
    [JsonPropertyName("Estado")]
    public ShipmentStatus Status { get; private set; }
    [JsonPropertyName("DireccionDestino")]
    public string DestinationAddress { get; private set; } = string.Empty;
    [JsonPropertyName("Transportadora")]
    public string Carrier { get; private set; } = string.Empty;
    [JsonPropertyName("GuiaSeguimiento")]
    public string TrackingNumber { get; private set; } = string.Empty;

    public StockMovement StockMovement { get; private set; } = null!;

    public Shipment() { }

    public Shipment(int movementId, string address, string carrier)
    {
        MovementId = movementId;
        DestinationAddress = address;
        Carrier = carrier;
        CreatedAt = DateTime.Now;
        Status = ShipmentStatus.Pending;
    }

    public void UpdateStatus(ShipmentStatus newStatus)
    {
        Status = newStatus;
    }
}

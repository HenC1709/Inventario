using System.Text.Json.Serialization;
using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.Domain.Entities
{
    public class StockMovement
    {
        public int Id {get; private set; }
        [JsonPropertyName("Fecha")]
        public DateTime Date { get; private set; }
        [JsonPropertyName("ProductoId")]
        public int ProductId { get; private set; }
        [JsonPropertyName("Cantidad")]
        public int Quantity { get; private set; }
        [JsonPropertyName("Tipo")]
        public MovementType MovementType { get; private set; }

        [JsonPropertyName("BodegaOrigenId")]
        public int? SourceWarehouseId { get; private set; }
        [JsonPropertyName("BodegaDestinoId")]
        public int? DestinationWarehouseId { get; private set; }

        [JsonPropertyName("Observaciones")]
        public string Notes { get; private set; } = string.Empty;
        public StockMovement() { }

        public StockMovement(int productId, int quantity, MovementType movementType, int? sourceWarehouseId, int? destinationWarehouseId, string notes)
        {
            Date = DateTime.Now;
            ProductId = productId;
            Quantity = quantity;
            MovementType = movementType;
            SourceWarehouseId = sourceWarehouseId;
            DestinationWarehouseId = destinationWarehouseId;
            Notes = notes;

        }
 
    }
}

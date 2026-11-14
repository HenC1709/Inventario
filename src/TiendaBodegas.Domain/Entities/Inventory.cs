namespace TiendaBodegas.Domain.Entities
{
    public class Inventory
    {
        public int Id { get; private set; }
        public int ProductId { get; private set; }
        public int WarehouseId { get; private set; }
        public int Quantity { get; private set; }
        public Product Product { get; private set; } = null!;
        public Warehouse Warehouse { get; private set; } = null!;

        public Inventory(int productId, int warehouseId, int quantity)
        {
            ProductId = productId;
            WarehouseId = warehouseId;
            Quantity = quantity;
        }
    }
}

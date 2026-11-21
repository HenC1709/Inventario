namespace TiendaBodegas.Domain.Entities;

/// <summary>
/// Represents a single-warehouse stock balance projection for a product.
/// </summary>
public class Inventory
{
    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public int Quantity { get; private set; }
    public Product Product { get; private set; } = null!;

    /// <summary>
    /// Initializes a stock balance entry for a product in the store's single warehouse.
    /// </summary>
    public Inventory(int productId, int quantity)
    {
        ProductId = productId;
        Quantity = quantity;
    }
}

using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Interfaces;
using TiendaBodegas.Domain.Enums;
namespace TiendaBodegas.Application.Services
{
    /// <summary>
    /// Manages inventory entries, exits, transfers, and stock queries across warehouses.
    /// </summary>
    public class InventoryManager : IInventoryService
    {
        private readonly IStockMovementRepository _movementRepository;
        private readonly IWarehouseRepository _warehouseRepository;
        private readonly IProductRepository? _productRepository;

        /// <summary>
        /// Initializes a new instance of <see cref="InventoryManager"/>.
        /// </summary>
        public InventoryManager(
            IStockMovementRepository movementRepository,
            IWarehouseRepository warehouseRepository,
            IProductRepository? productRepository = null)
        {
            _movementRepository = movementRepository;
            _warehouseRepository = warehouseRepository;
            _productRepository = productRepository;
        }

        /// <summary>
        /// Transfers a positive quantity of a product between two distinct warehouses after verifying available stock.
        /// </summary>
        public void TransferStock(int productId, int sourceWarehouseId, int destinationWarehouseId, int quantity, string notes)
        {
            ValidateQuantity(quantity);
            ValidateProduct(productId);

            if (sourceWarehouseId == destinationWarehouseId)
                throw new ArgumentException("The source and destination warehouses must be different.");

            var sourceWarehouse = _warehouseRepository.GetById(sourceWarehouseId);
            var destinationWarehouse = _warehouseRepository.GetById(destinationWarehouseId);

            if (sourceWarehouse == null || destinationWarehouse == null)
                throw new ArgumentException("One or both warehouses do not exist.");

            var sourceStock = GetProductStockInWarehouse(productId, sourceWarehouseId);

            if (sourceStock < quantity)
                throw new InvalidOperationException(
                    $"Insufficient stock in the source warehouse. Available: {sourceStock}, requested: {quantity}.");

            var newMovement = new StockMovement(
                productId, quantity,
                MovementType.Transfer,
                sourceWarehouseId, destinationWarehouseId, notes ?? string.Empty
            );
            _movementRepository.Add(newMovement);
        }

        /// <summary>
        /// Registers a positive stock entry into an existing warehouse for a valid product.
        /// </summary>
        public void RegisterEntry(int productId, int warehouseId, int quantity, string notes)
        {
            ValidateQuantity(quantity);
            ValidateProduct(productId);
            ValidateWarehouse(warehouseId, nameof(warehouseId));

            var newMovement = new StockMovement(
                productId, quantity,
                MovementType.Entry,
                null, warehouseId, notes ?? string.Empty
            );
            _movementRepository.Add(newMovement);
        }

        /// <summary>
        /// Registers a positive stock exit from an existing warehouse after verifying sufficient available stock.
        /// </summary>
        public void RegisterExit(int productId, int warehouseId, int quantity, string notes)
        {
            ValidateQuantity(quantity);
            ValidateProduct(productId);
            ValidateWarehouse(warehouseId, nameof(warehouseId));

            var availableStock = GetProductStockInWarehouse(productId, warehouseId);
            if (availableStock < quantity)
                throw new InvalidOperationException(
                    $"Insufficient stock in the warehouse. Available: {availableStock}, requested: {quantity}.");

            var newMovement = new StockMovement(
                productId, quantity,
                MovementType.Exit,
                warehouseId, null, notes ?? string.Empty
            );
            _movementRepository.Add(newMovement);
        }

        /// <summary>
        /// Returns the current stock balance for every product with movements in the given warehouse.
        /// </summary>
        public IEnumerable<Inventory> GetStockByWarehouse(int warehouseId)
        {
            var movements = _movementRepository.GetByWarehouseId(warehouseId);
            return movements.GroupBy(movement => movement.ProductId)
                .Select(g => new Inventory(
                    g.Key,
                    warehouseId,
                    g.Sum(movement => GetSignedQuantity(movement, warehouseId))
                ));
        }

        /// <summary>
        /// Calculates the net global stock for a product across all warehouses (entries minus exits).
        /// </summary>
        public int GetTotalStockByProduct(int productId)
        {
            var entries = _movementRepository.GetByMovementType(MovementType.Entry)
                .Where(movement => movement.ProductId == productId)
                .Sum(movement => movement.Quantity);

            var exits = _movementRepository.GetByMovementType(MovementType.Exit)
                .Where(movement => movement.ProductId == productId)
                .Sum(movement => movement.Quantity);

            return entries - exits;
        }

        /// <summary>
        /// Ensures that the movement quantity is strictly positive.
        /// </summary>
        private static void ValidateQuantity(int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        /// <summary>
        /// Validates the product identifier and, when a product repository is available, checks that the product exists.
        /// </summary>
        private void ValidateProduct(int productId)
        {
            if (productId <= 0)
                throw new ArgumentOutOfRangeException(nameof(productId), "Product id must be greater than zero.");

            if (_productRepository != null && _productRepository.GetById(productId) == null)
                throw new ArgumentException("The specified product does not exist.", nameof(productId));
        }

        /// <summary>
        /// Validates that the referenced warehouse exists.
        /// </summary>
        private void ValidateWarehouse(int warehouseId, string paramName)
        {
            if (_warehouseRepository.GetById(warehouseId) == null)
                throw new ArgumentException("The specified warehouse does not exist.", paramName);
        }

        /// <summary>
        /// Computes the current stock balance of a product in a specific warehouse from its movement history.
        /// </summary>
        private int GetProductStockInWarehouse(int productId, int warehouseId)
        {
            return _movementRepository.GetByWarehouseId(warehouseId)
                .Where(movement => movement.ProductId == productId)
                .Sum(movement => GetSignedQuantity(movement, warehouseId));
        }

        /// <summary>
        /// Computes the signed stock delta of a movement relative to the given warehouse.
        /// </summary>
        private static int GetSignedQuantity(StockMovement movement, int warehouseId)
        {
            return movement.MovementType switch
            {
                MovementType.Entry => movement.Quantity,
                MovementType.Exit => -movement.Quantity,
                _ => movement.DestinationWarehouseId == warehouseId ? movement.Quantity : -movement.Quantity
            };
        }
    }
}

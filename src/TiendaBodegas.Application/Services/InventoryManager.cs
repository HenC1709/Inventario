using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Interfaces;
using TiendaBodegas.Domain.Enums;
namespace TiendaBodegas.Application.Services
{
    public class InventoryManager : IInventoryService
    {
        private readonly IStockMovementRepository _movementRepository;
        private readonly IWarehouseRepository _warehouseRepository;

        public InventoryManager(IStockMovementRepository movementRepository, IWarehouseRepository warehouseRepository)
        {
            _movementRepository = movementRepository;
            _warehouseRepository = warehouseRepository;
        }
        public void TransferStock(int productId, int sourceWarehouseId, int destinationWarehouseId, int quantity, string notes)
        {
            if (sourceWarehouseId == destinationWarehouseId)
                throw new ArgumentException("The source and destination warehouses must be different.");

            var sourceWarehouse = _warehouseRepository.GetById(sourceWarehouseId);
            var destinationWarehouse = _warehouseRepository.GetById(destinationWarehouseId);

            if (sourceWarehouse == null || destinationWarehouse == null)
                throw new ArgumentException("One or both warehouses do not exist.");

            var sourceStock = _movementRepository.GetByWarehouseId(sourceWarehouseId)
               .Where(t => t.ProductId == productId)
               .Sum(t => t.MovementType == MovementType.Entry ? t.Quantity :
               t.MovementType == MovementType.Exit ? -t.Quantity :
               t.DestinationWarehouseId == sourceWarehouseId ? t.Quantity : -t.Quantity);

            if (sourceStock < quantity)
                throw new InvalidOperationException(
                    $"Insufficient stock in the source warehouse. Available: {sourceStock}, requested: {quantity}.");

            var newMovement = new StockMovement(
                productId, quantity,
                MovementType.Transfer,
                sourceWarehouseId, destinationWarehouseId, notes
            );
            _movementRepository.Add(newMovement);
        }
        public void RegisterEntry(int productId, int warehouseId, int quantity, string notes)
        {
            var newMovement = new StockMovement(
                productId, quantity,
                MovementType.Entry,
                null, warehouseId, notes
            );
            _movementRepository.Add(newMovement);
        }
        public void RegisterExit(int productId, int warehouseId, int quantity, string notes)
        {
            var newMovement = new StockMovement(
                productId, quantity,
                MovementType.Exit,
                warehouseId, null, notes
            );
            _movementRepository.Add(newMovement);
        }

        public IEnumerable<Inventory> GetStockByWarehouse(int warehouseId)
        {
            var movements = _movementRepository.GetByWarehouseId(warehouseId);
            return movements.GroupBy(movement => movement.ProductId)
            .Select(g => new Inventory(
             g.Key, warehouseId,
             g.Sum(movement => movement.MovementType == MovementType.Entry ? movement.Quantity :
             movement.MovementType == MovementType.Exit ? -movement.Quantity :
             movement.DestinationWarehouseId == warehouseId ? movement.Quantity : -movement.Quantity)
            ));
        }

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
    }
}

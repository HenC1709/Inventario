using TiendaBodegas.Application.DTOs;
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
        /// Registers an entry, exit, or transfer movement from a unified request object.
        /// </summary>
        public void RegisterMovement(StockMovementRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            switch (request.MovementType)
            {
                case MovementType.Entry:
                    if (!request.DestinationWarehouseId.HasValue)
                        throw new ArgumentException("Debe seleccionar una bodega de destino.", nameof(request));

                    RegisterEntry(request.ProductId, request.DestinationWarehouseId.Value, request.Quantity, request.Notes);
                    break;

                case MovementType.Exit:
                    if (!request.SourceWarehouseId.HasValue)
                        throw new ArgumentException("Debe seleccionar una bodega de origen.", nameof(request));

                    RegisterExit(request.ProductId, request.SourceWarehouseId.Value, request.Quantity, request.Notes);
                    break;

                case MovementType.Transfer:
                    if (!request.SourceWarehouseId.HasValue)
                        throw new ArgumentException("Debe seleccionar una bodega de origen.", nameof(request));

                    if (!request.DestinationWarehouseId.HasValue)
                        throw new ArgumentException("Debe seleccionar una bodega de destino.", nameof(request));

                    TransferStock(
                        request.ProductId,
                        request.SourceWarehouseId.Value,
                        request.DestinationWarehouseId.Value,
                        request.Quantity,
                        request.Notes);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(request), "El tipo de movimiento especificado no es válido.");
            }
        }

        /// <summary>
        /// Transfers a positive quantity of a product between two distinct warehouses after verifying available stock.
        /// </summary>
        public void TransferStock(int productId, int sourceWarehouseId, int destinationWarehouseId, int quantity, string notes)
        {
            ValidateQuantity(quantity);
            ValidateProduct(productId);

            if (sourceWarehouseId == destinationWarehouseId)
                throw new ArgumentException("La bodega de origen y la de destino deben ser diferentes.");

            var sourceWarehouse = _warehouseRepository.GetById(sourceWarehouseId);
            var destinationWarehouse = _warehouseRepository.GetById(destinationWarehouseId);

            if (sourceWarehouse == null || destinationWarehouse == null)
                throw new ArgumentException("Una o ambas bodegas especificadas no existen.");

            var sourceStock = GetProductStockInWarehouse(productId, sourceWarehouseId);

            if (sourceStock < quantity)
                throw new InvalidOperationException(
                    $"Stock insuficiente en la bodega de origen. Disponible: {sourceStock}, solicitado: {quantity}.");

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
                    $"Stock insuficiente en la bodega. Disponible: {availableStock}, solicitado: {quantity}.");

            var newMovement = new StockMovement(
                productId, quantity,
                MovementType.Exit,
                warehouseId, null, notes ?? string.Empty
            );
            _movementRepository.Add(newMovement);
        }

        /// <summary>
        /// Retrieves stock movements matching optional filter criteria, ordered from newest to oldest.
        /// </summary>
        public IEnumerable<StockMovement> GetMovements(StockMovementFilter? filter = null)
        {
            IEnumerable<StockMovement> query = _movementRepository.GetAll();

            if (filter != null)
            {
                if (filter.FromDate.HasValue)
                {
                    var fromDate = filter.FromDate.Value;
                    query = query.Where(m => m.Date >= fromDate);
                }

                if (filter.ToDate.HasValue)
                {
                    var toDate = filter.ToDate.Value;
                    query = toDate.TimeOfDay == TimeSpan.Zero
                        ? query.Where(m => m.Date < toDate.Date.AddDays(1))
                        : query.Where(m => m.Date <= toDate);
                }

                if (filter.MovementType.HasValue)
                {
                    var movementType = filter.MovementType.Value;
                    query = query.Where(m => m.MovementType == movementType);
                }

                if (filter.WarehouseId.HasValue)
                {
                    var warehouseId = filter.WarehouseId.Value;
                    query = query.Where(m =>
                        m.SourceWarehouseId == warehouseId ||
                        m.DestinationWarehouseId == warehouseId);
                }

                if (filter.ProductId.HasValue)
                {
                    var productId = filter.ProductId.Value;
                    query = query.Where(m => m.ProductId == productId);
                }
            }

            return query
                .OrderByDescending(m => m.Date)
                .ThenByDescending(m => m.Id)
                .ToList();
        }

        /// <summary>
        /// Retrieves enriched stock movements with readable product and warehouse labels matching optional filters.
        /// </summary>
        public IEnumerable<StockMovementDisplayDto> GetMovementDetails(StockMovementFilter? filter = null)
        {
            var movements = GetMovements(filter);
            var productsById = (_productRepository?.GetAll() ?? Enumerable.Empty<Product>())
                .ToDictionary(p => p.Id);
            var warehousesById = _warehouseRepository.GetAll()
                .ToDictionary(w => w.Id);

            return movements.Select(m =>
            {
                var productName = productsById.TryGetValue(m.ProductId, out var product)
                    ? product.Name
                    : $"Producto #{m.ProductId}";

                var sourceWarehouseName = m.SourceWarehouseId.HasValue
                    ? (warehousesById.TryGetValue(m.SourceWarehouseId.Value, out var source)
                        ? source.Name
                        : $"Bodega #{m.SourceWarehouseId.Value}")
                    : string.Empty;

                var destinationWarehouseName = m.DestinationWarehouseId.HasValue
                    ? (warehousesById.TryGetValue(m.DestinationWarehouseId.Value, out var dest)
                        ? dest.Name
                        : $"Bodega #{m.DestinationWarehouseId.Value}")
                    : string.Empty;

                var movementTypeLabel = m.MovementType switch
                {
                    MovementType.Entry => "Entrada",
                    MovementType.Exit => "Salida",
                    MovementType.Transfer => "Traslado",
                    _ => m.MovementType.ToString()
                };

                return new StockMovementDisplayDto(
                    m.Id,
                    m.Date,
                    m.MovementType,
                    movementTypeLabel,
                    m.ProductId,
                    productName,
                    $"#{m.ProductId} — {productName}",
                    m.Quantity,
                    m.SourceWarehouseId,
                    sourceWarehouseName,
                    m.SourceWarehouseId.HasValue ? $"#{m.SourceWarehouseId.Value} — {sourceWarehouseName}" : "—",
                    m.DestinationWarehouseId,
                    destinationWarehouseName,
                    m.DestinationWarehouseId.HasValue ? $"#{m.DestinationWarehouseId.Value} — {destinationWarehouseName}" : "—",
                    m.Notes);
            }).ToList();
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
        /// Computes the current stock balance of a product in a specific warehouse from its movement history.
        /// </summary>
        public int GetProductStockInWarehouse(int productId, int warehouseId)
        {
            return _movementRepository.GetByWarehouseId(warehouseId)
                .Where(movement => movement.ProductId == productId)
                .Sum(movement => GetSignedQuantity(movement, warehouseId));
        }

        /// <summary>
        /// Builds a stock summary report per warehouse and product, including low-stock flags.
        /// </summary>
        public IEnumerable<WarehouseStockDto> GetWarehouseStockReport(int? warehouseId = null)
        {
            var warehouses = warehouseId.HasValue
                ? _warehouseRepository.GetAll().Where(w => w.Id == warehouseId.Value)
                : _warehouseRepository.GetAll();

            var productsById = (_productRepository?.GetAll() ?? Enumerable.Empty<Product>())
                .ToDictionary(p => p.Id);

            var totalStockByProduct = new Dictionary<int, int>();
            var rows = new List<WarehouseStockDto>();

            foreach (var warehouse in warehouses)
            {
                foreach (var item in GetStockByWarehouse(warehouse.Id))
                {
                    productsById.TryGetValue(item.ProductId, out var product);

                    if (!totalStockByProduct.TryGetValue(item.ProductId, out var totalStock))
                    {
                        totalStock = GetTotalStockByProduct(item.ProductId);
                        totalStockByProduct[item.ProductId] = totalStock;
                    }

                    var minStock = product?.MinStock ?? 0;
                    var productName = product?.Name ?? $"Producto #{item.ProductId}";
                    var productSku = product?.SKU ?? string.Empty;

                    rows.Add(new WarehouseStockDto(
                        warehouse.Id,
                        warehouse.Name,
                        item.ProductId,
                        productSku,
                        productName,
                        minStock,
                        item.Quantity,
                        totalStock,
                        item.Quantity <= minStock));
                }
            }

            return rows
                .OrderBy(r => r.WarehouseName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.ProductName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Builds a global stock summary per product across all warehouses, including low-stock flags.
        /// </summary>
        public IEnumerable<ProductStockSummaryDto> GetProductStockSummary()
        {
            var products = _productRepository?.GetAll() ?? Enumerable.Empty<Product>();

            return products.Select(product =>
            {
                var totalStock = GetTotalStockByProduct(product.Id);
                return new ProductStockSummaryDto(
                    product.Id,
                    product.SKU,
                    product.Name,
                    product.MinStock,
                    totalStock,
                    totalStock <= product.MinStock);
            })
            .OrderBy(p => p.ProductName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        }

        /// <summary>
        /// Ensures that the movement quantity is strictly positive.
        /// </summary>
        private static void ValidateQuantity(int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity), "La cantidad debe ser mayor a cero.");
        }

        /// <summary>
        /// Validates the product identifier and, when a product repository is available, checks that the product exists.
        /// </summary>
        private void ValidateProduct(int productId)
        {
            if (productId <= 0)
                throw new ArgumentOutOfRangeException(nameof(productId), "El identificador del producto debe ser mayor a cero.");

            if (_productRepository != null && _productRepository.GetById(productId) == null)
                throw new ArgumentException("El producto especificado no existe.", nameof(productId));
        }

        /// <summary>
        /// Validates that the referenced warehouse exists.
        /// </summary>
        private void ValidateWarehouse(int warehouseId, string paramName)
        {
            if (_warehouseRepository.GetById(warehouseId) == null)
                throw new ArgumentException("La bodega especificada no existe.", paramName);
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

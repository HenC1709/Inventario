using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Enums;
using TiendaBodegas.Domain.Interfaces;

namespace TiendaBodegas.Application.Services;

/// <summary>
/// Manages single-warehouse inventory entries, exits, and stock summary projections.
/// </summary>
public class InventoryManager : IInventoryService
{
    private readonly IStockMovementRepository _movementRepository;
    private readonly IProductRepository _productRepository;
    private readonly ISupplierRepository? _supplierRepository;

    /// <summary>
    /// Initializes a new instance of <see cref="InventoryManager"/>.
    /// </summary>
    public InventoryManager(
        IStockMovementRepository movementRepository,
        IProductRepository productRepository,
        ISupplierRepository? supplierRepository = null)
    {
        _movementRepository = movementRepository;
        _productRepository = productRepository;
        _supplierRepository = supplierRepository;
    }

    /// <summary>
    /// Registers an entry or exit movement from a unified request object.
    /// </summary>
    public void RegisterMovement(StockMovementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        switch (request.MovementType)
        {
            case MovementType.Entry:
                RegisterEntry(request.ProductId, request.Quantity, request.Notes);
                break;

            case MovementType.Exit:
                RegisterExit(request.ProductId, request.Quantity, request.Notes);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(request), "El tipo de movimiento especificado no es válido.");
        }
    }

    /// <summary>
    /// Registers a positive stock entry for a valid product.
    /// </summary>
    public void RegisterEntry(int productId, int quantity, string notes)
    {
        ValidateQuantity(quantity);
        ValidateProduct(productId);

        var newMovement = new StockMovement(
            productId,
            quantity,
            MovementType.Entry,
            notes ?? string.Empty);

        _movementRepository.Add(newMovement);
    }

    /// <summary>
    /// Registers a positive stock exit for a valid product after verifying sufficient available stock.
    /// </summary>
    public void RegisterExit(int productId, int quantity, string notes)
    {
        ValidateQuantity(quantity);
        ValidateProduct(productId);

        var availableStock = GetTotalStockByProduct(productId);
        if (availableStock < quantity)
        {
            throw new InvalidOperationException(
                $"Stock insuficiente. Disponible: {availableStock}, solicitado: {quantity}.");
        }

        var newMovement = new StockMovement(
            productId,
            quantity,
            MovementType.Exit,
            notes ?? string.Empty);

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
    /// Retrieves enriched stock movements with product labels and valuation matching optional filters.
    /// </summary>
    public IEnumerable<StockMovementDisplayDto> GetMovementDetails(StockMovementFilter? filter = null)
    {
        var movements = GetMovements(filter);
        var productsById = _productRepository.GetAll()
            .ToDictionary(p => p.Id);

        return movements.Select(m =>
        {
            var hasProduct = productsById.TryGetValue(m.ProductId, out var product);
            var productName = hasProduct ? product!.Name : $"Producto #{m.ProductId}";
            var productSku = hasProduct ? product!.SKU : string.Empty;
            var unitPrice = hasProduct
                ? (m.MovementType == MovementType.Exit ? product!.SalePrice : product!.PurchasePrice)
                : 0m;
            var totalAmount = unitPrice * m.Quantity;

            var movementTypeLabel = m.MovementType switch
            {
                MovementType.Entry => "Entrada",
                MovementType.Exit => "Salida",
                _ => m.MovementType.ToString()
            };

            return new StockMovementDisplayDto(
                m.Id,
                m.Date,
                m.MovementType,
                movementTypeLabel,
                m.ProductId,
                productSku,
                productName,
                $"#{m.ProductId} — {productName}",
                m.Quantity,
                unitPrice,
                totalAmount,
                m.Notes);
        }).ToList();
    }

    /// <summary>
    /// Calculates the net global stock for a product (entries minus exits).
    /// </summary>
    public int GetTotalStockByProduct(int productId)
    {
        return _movementRepository.GetByProductId(productId)
            .Sum(GetSignedQuantity);
    }

    /// <summary>
    /// Builds a global stock summary per product including supplier info, pricing, and semantic stock status labels.
    /// </summary>
    public IEnumerable<ProductStockSummaryDto> GetProductStockSummary()
    {
        var products = _productRepository.GetAll().ToList();
        var suppliersById = (_supplierRepository?.GetAll() ?? Enumerable.Empty<Supplier>())
            .ToDictionary(s => s.Id);

        var stockByProduct = _movementRepository.GetAll()
            .Where(m => m.MovementType is MovementType.Entry or MovementType.Exit)
            .GroupBy(m => m.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(GetSignedQuantity));

        return products.Select(product =>
        {
            var totalStock = stockByProduct.GetValueOrDefault(product.Id, 0);
            var isOutOfStock = totalStock <= 0;
            var isLowStock = totalStock <= product.MinStock;
            var statusLabel = isOutOfStock
                ? "Sin Stock"
                : (isLowStock ? "Bajo Stock" : "En Stock");

            var supplierName = product.SupplierId.HasValue
                && suppliersById.TryGetValue(product.SupplierId.Value, out var supplier)
                    ? supplier.Name
                    : "Sin proveedor";

            return new ProductStockSummaryDto(
                product.Id,
                product.SKU,
                product.Name,
                product.Category,
                product.SupplierId,
                supplierName,
                product.PurchasePrice,
                product.SalePrice,
                product.MinStock,
                totalStock,
                isOutOfStock,
                isLowStock,
                statusLabel);
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
    /// Validates the product identifier and checks that the referenced product exists.
    /// </summary>
    private void ValidateProduct(int productId)
    {
        if (productId <= 0)
            throw new ArgumentOutOfRangeException(nameof(productId), "El identificador del producto debe ser mayor a cero.");

        if (_productRepository.GetById(productId) == null)
            throw new ArgumentException("El producto especificado no existe.", nameof(productId));
    }

    /// <summary>
    /// Computes the signed stock delta of a single-warehouse movement.
    /// </summary>
    private static int GetSignedQuantity(StockMovement movement)
    {
        return movement.MovementType switch
        {
            MovementType.Entry => movement.Quantity,
            MovementType.Exit => -movement.Quantity,
            _ => 0
        };
    }
}

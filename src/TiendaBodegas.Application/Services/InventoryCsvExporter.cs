using System.Globalization;
using System.Text;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;

namespace TiendaBodegas.Application.Services;

/// <summary>
/// Generates RFC-4180 compliant CSV content from inventory, valuation, and low-rotation projections.
/// </summary>
public class InventoryCsvExporter : IInventoryExportService
{
    private const string InventoryHeader = "SKU,Producto,Categoría,Proveedor,Precio Compra,Precio Venta,Stock,Stock Mínimo,Estado";
    private const string ValuationHeader = "Categoría,Productos,Stock Total,Costo Compra Total,Valor Venta Total,Margen Estimado";
    private const string LowRotationHeader = "SKU,Producto,Categoría,Stock Actual,Último Movimiento,Días Sin Rotación";

    /// <summary>
    /// Builds a CSV document containing headers and one formatted row per inventory item.
    /// </summary>
    public string GenerateInventoryCsv(IEnumerable<ProductStockSummaryDto> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var builder = new StringBuilder();
        builder.AppendLine(InventoryHeader);

        foreach (var item in items)
        {
            var fields = new[]
            {
                EscapeCsvField(item.ProductSku),
                EscapeCsvField(item.ProductName),
                EscapeCsvField(item.Category),
                EscapeCsvField(item.SupplierName),
                item.PurchasePrice.ToString("0.00", CultureInfo.InvariantCulture),
                item.SalePrice.ToString("0.00", CultureInfo.InvariantCulture),
                item.TotalStock.ToString(CultureInfo.InvariantCulture),
                item.MinStock.ToString(CultureInfo.InvariantCulture),
                EscapeCsvField(item.StockStatusLabel)
            };

            builder.AppendLine(string.Join(",", fields));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Builds a CSV document summarizing category valuation and a <c>TOTAL</c> summary row when rows exist.
    /// </summary>
    public string GenerateValuationCsv(IEnumerable<CategoryValuationDto> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);

        var rows = categories.ToList();
        var builder = new StringBuilder();
        builder.AppendLine(ValuationHeader);

        foreach (var row in rows)
        {
            var fields = new[]
            {
                EscapeCsvField(row.Category),
                row.ProductCount.ToString(CultureInfo.InvariantCulture),
                row.TotalUnits.ToString(CultureInfo.InvariantCulture),
                row.TotalPurchaseCost.ToString("0.00", CultureInfo.InvariantCulture),
                row.TotalSaleValue.ToString("0.00", CultureInfo.InvariantCulture),
                row.EstimatedMargin.ToString("0.00", CultureInfo.InvariantCulture)
            };

            builder.AppendLine(string.Join(",", fields));
        }

        if (rows.Count > 0)
        {
            var totalFields = new[]
            {
                "TOTAL",
                rows.Sum(r => r.ProductCount).ToString(CultureInfo.InvariantCulture),
                rows.Sum(r => r.TotalUnits).ToString(CultureInfo.InvariantCulture),
                rows.Sum(r => r.TotalPurchaseCost).ToString("0.00", CultureInfo.InvariantCulture),
                rows.Sum(r => r.TotalSaleValue).ToString("0.00", CultureInfo.InvariantCulture),
                rows.Sum(r => r.EstimatedMargin).ToString("0.00", CultureInfo.InvariantCulture)
            };

            builder.AppendLine(string.Join(",", totalFields));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Builds a CSV document listing low-rotation products and their last movement timestamp.
    /// </summary>
    public string GenerateLowRotationCsv(IEnumerable<LowRotationProductDto> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var builder = new StringBuilder();
        builder.AppendLine(LowRotationHeader);

        foreach (var item in items)
        {
            var lastMovementText = item.LastMovementDate.HasValue
                ? item.LastMovementDate.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                : "Sin movimientos";
            var daysText = item.DaysWithoutMovement.HasValue
                ? item.DaysWithoutMovement.Value.ToString(CultureInfo.InvariantCulture)
                : "—";

            var fields = new[]
            {
                EscapeCsvField(item.ProductSku),
                EscapeCsvField(item.ProductName),
                EscapeCsvField(item.Category),
                item.CurrentStock.ToString(CultureInfo.InvariantCulture),
                EscapeCsvField(lastMovementText),
                EscapeCsvField(daysText)
            };

            builder.AppendLine(string.Join(",", fields));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Escapes a CSV field according to RFC-4180 when it contains commas, quotes, or line breaks.
    /// </summary>
    private static string EscapeCsvField(string? value)
    {
        var text = value ?? string.Empty;
        if (!text.Contains(',') && !text.Contains('"') && !text.Contains('\r') && !text.Contains('\n'))
            return text;

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}

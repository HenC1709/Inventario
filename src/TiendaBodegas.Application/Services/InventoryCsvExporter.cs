using System.Globalization;
using System.Text;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;

namespace TiendaBodegas.Application.Services;

/// <summary>
/// Generates RFC-4180 compliant CSV content from <see cref="ProductStockSummaryDto"/> inventory records.
/// </summary>
public class InventoryCsvExporter : IInventoryExportService
{
    private const string CsvHeader = "SKU,Producto,Categoría,Proveedor,Precio Compra,Precio Venta,Stock,Stock Mínimo,Estado";

    /// <summary>
    /// Builds a CSV document containing headers and one formatted row per inventory item.
    /// </summary>
    public string GenerateInventoryCsv(IEnumerable<ProductStockSummaryDto> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var builder = new StringBuilder();
        builder.AppendLine(CsvHeader);

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

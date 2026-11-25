using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Application.Services;

namespace TiendaBodegas.Infrastructure.Services;

/// <summary>
/// Generates formatted Excel (.xlsx) workbooks using ClosedXML from inventory, valuation, and low-rotation DTOs.
/// </summary>
public class ClosedXmlExcelExportService : IExcelExportService
{
    private const string DefaultBusinessName = "Almacén Central";
    private const string BrandPrimaryHex = "#1E4E79";
    private const string BrandAccentHex = "#C86D44";
    private const string SurfaceSoftHex = "#F9F7F4";
    private const string BorderLightHex = "#E5DFD7";
    private const string TextPrimaryHex = "#1C1917";
    private const string TextMutedHex = "#78716C";
    private const string SuccessBgHex = "#D1FAE5";
    private const string SuccessTextHex = "#065F46";
    private const string WarningBgHex = "#FDE8D8";
    private const string WarningTextHex = "#843E20";
    private const string DangerBgHex = "#FEE2E2";
    private const string DangerTextHex = "#B91C1C";
    private const string IntegerFormat = "#,##0";

    private readonly ISettingsService? _settingsService;

    /// <summary>
    /// Initializes a new instance of <see cref="ClosedXmlExcelExportService"/>.
    /// </summary>
    public ClosedXmlExcelExportService(ISettingsService? settingsService = null)
    {
        _settingsService = settingsService;
    }

    /// <inheritdoc />
    public byte[] GenerateValuationExcel(
        IEnumerable<CategoryValuationDto> categories,
        string? businessName = null,
        string? currencyCode = null)
    {
        ArgumentNullException.ThrowIfNull(categories);

        var rows = categories.ToList();
        var (resolvedBusiness, resolvedCurrency) = ResolveContext(businessName, currencyCode);
        var currencyFormat = GetCurrencyFormat(resolvedCurrency);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Valoración");

        ComposeSheetHeader(
            sheet,
            resolvedBusiness,
            "Reporte de Valoración de Inventario por Categoría",
            $"Moneda activa: {resolvedCurrency}",
            columnCount: 6);

        var headers = new[]
        {
            "CATEGORÍA",
            "PRODUCTOS",
            "STOCK TOTAL",
            $"COSTO COMPRA ({resolvedCurrency})",
            $"VALOR VENTA ({resolvedCurrency})",
            $"MARGEN ESTIMADO ({resolvedCurrency})"
        };

        WriteTableHeader(sheet, rowIndex: 5, headers, rightAlignedColumns: [2, 3, 4, 5, 6]);

        var currentRow = 6;
        if (rows.Count == 0)
        {
            WriteEmptyNotice(sheet, currentRow, "No hay datos de valoración registrados.", columnCount: 6);
        }
        else
        {
            foreach (var row in rows)
            {
                sheet.Cell(currentRow, 1).Value = row.Category;
                sheet.Cell(currentRow, 1).Style.Font.Bold = true;

                SetIntegerCell(sheet.Cell(currentRow, 2), row.ProductCount);
                SetIntegerCell(sheet.Cell(currentRow, 3), row.TotalUnits);
                SetCurrencyCell(sheet.Cell(currentRow, 4), row.TotalPurchaseCost, currencyFormat);
                SetCurrencyCell(sheet.Cell(currentRow, 5), row.TotalSaleValue, currencyFormat);
                SetCurrencyCell(sheet.Cell(currentRow, 6), row.EstimatedMargin, currencyFormat);

                StyleDataRow(sheet, currentRow, columnCount: 6);
                currentRow++;
            }

            sheet.Cell(currentRow, 1).Value = "TOTAL";
            SetIntegerCell(sheet.Cell(currentRow, 2), rows.Sum(r => r.ProductCount));
            SetIntegerCell(sheet.Cell(currentRow, 3), rows.Sum(r => r.TotalUnits));
            SetCurrencyCell(sheet.Cell(currentRow, 4), rows.Sum(r => r.TotalPurchaseCost), currencyFormat);
            SetCurrencyCell(sheet.Cell(currentRow, 5), rows.Sum(r => r.TotalSaleValue), currencyFormat);
            SetCurrencyCell(sheet.Cell(currentRow, 6), rows.Sum(r => r.EstimatedMargin), currencyFormat);
            StyleTotalRow(sheet, currentRow, columnCount: 6);
        }

        ConfigureSheetLayout(sheet, headerRow: 5, lastTableRow: Math.Max(5, rows.Count + 5), columnWidths: [28, 15, 16, 22, 22, 24]);
        return SaveToBytes(workbook);
    }

    /// <inheritdoc />
    public byte[] GenerateInventoryExcel(
        IEnumerable<ProductStockSummaryDto> items,
        string? businessName = null,
        string? currencyCode = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        var rows = items.ToList();
        var (resolvedBusiness, resolvedCurrency) = ResolveContext(businessName, currencyCode);
        var currencyFormat = GetCurrencyFormat(resolvedCurrency);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Inventario");

        ComposeSheetHeader(
            sheet,
            resolvedBusiness,
            "Reporte General de Inventario de Productos",
            $"Productos listados: {rows.Count} • Moneda activa: {resolvedCurrency}",
            columnCount: 9);

        var headers = new[]
        {
            "SKU",
            "PRODUCTO",
            "CATEGORÍA",
            "PROVEEDOR",
            $"PRECIO COMPRA ({resolvedCurrency})",
            $"PRECIO VENTA ({resolvedCurrency})",
            "STOCK",
            "STOCK MÍNIMO",
            "ESTADO"
        };

        WriteTableHeader(sheet, rowIndex: 5, headers, rightAlignedColumns: [5, 6, 7, 8], centerAlignedColumns: [9]);

        var currentRow = 6;
        if (rows.Count == 0)
        {
            WriteEmptyNotice(sheet, currentRow, "No hay productos registrados para mostrar.", columnCount: 9);
        }
        else
        {
            foreach (var item in rows)
            {
                sheet.Cell(currentRow, 1).Value = item.ProductSku;
                sheet.Cell(currentRow, 2).Value = item.ProductName;
                sheet.Cell(currentRow, 2).Style.Font.Bold = true;
                sheet.Cell(currentRow, 3).Value = item.Category;
                sheet.Cell(currentRow, 4).Value = item.SupplierName;

                SetCurrencyCell(sheet.Cell(currentRow, 5), item.PurchasePrice, currencyFormat);
                SetCurrencyCell(sheet.Cell(currentRow, 6), item.SalePrice, currencyFormat);
                SetIntegerCell(sheet.Cell(currentRow, 7), item.TotalStock);
                SetIntegerCell(sheet.Cell(currentRow, 8), item.MinStock);

                var statusCell = sheet.Cell(currentRow, 9);
                statusCell.Value = item.StockStatusLabel;
                statusCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                statusCell.Style.Font.Bold = true;

                StyleDataRow(sheet, currentRow, columnCount: 9);
                ApplyStatusColors(statusCell, item.StockStatusLabel);
                currentRow++;
            }

            sheet.Cell(currentRow, 1).Value = "TOTAL";
            sheet.Cell(currentRow, 2).Value = $"{rows.Count} producto(s)";
            SetIntegerCell(sheet.Cell(currentRow, 7), rows.Sum(r => r.TotalStock));
            StyleTotalRow(sheet, currentRow, columnCount: 9);
        }

        ConfigureSheetLayout(sheet, headerRow: 5, lastTableRow: Math.Max(5, rows.Count + 5), columnWidths: [16, 34, 18, 28, 22, 22, 12, 15, 16]);
        return SaveToBytes(workbook);
    }

    /// <inheritdoc />
    public byte[] GenerateLowRotationExcel(
        IEnumerable<LowRotationProductDto> items,
        string? businessName = null,
        int auditDays = 30)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (auditDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(auditDays), "El período de auditoría debe ser mayor a cero.");

        var rows = items.ToList();
        var (resolvedBusiness, _) = ResolveContext(businessName, currencyCode: null);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Rotación Baja");

        ComposeSheetHeader(
            sheet,
            resolvedBusiness,
            $"Auditoría de Productos con Rotación Baja ({auditDays} días)",
            $"Productos detectados: {rows.Count}",
            columnCount: 6);

        var headers = new[]
        {
            "SKU",
            "PRODUCTO",
            "CATEGORÍA",
            "STOCK ACTUAL",
            "ÚLTIMO MOVIMIENTO",
            "DÍAS SIN ROTACIÓN"
        };

        WriteTableHeader(sheet, rowIndex: 5, headers, rightAlignedColumns: [4, 6], centerAlignedColumns: [5]);

        var currentRow = 6;
        if (rows.Count == 0)
        {
            WriteEmptyNotice(
                sheet,
                currentRow,
                $"Sin novedades: todos los productos registraron movimientos en los últimos {auditDays} días.",
                columnCount: 6);
        }
        else
        {
            foreach (var item in rows)
            {
                sheet.Cell(currentRow, 1).Value = item.ProductSku;
                sheet.Cell(currentRow, 2).Value = item.ProductName;
                sheet.Cell(currentRow, 2).Style.Font.Bold = true;
                sheet.Cell(currentRow, 3).Value = item.Category;

                SetIntegerCell(sheet.Cell(currentRow, 4), item.CurrentStock);

                var lastMoveCell = sheet.Cell(currentRow, 5);
                lastMoveCell.Value = item.LastMovementDate.HasValue
                    ? item.LastMovementDate.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                    : "Sin movimientos";
                lastMoveCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var daysCell = sheet.Cell(currentRow, 6);
                if (item.DaysWithoutMovement.HasValue)
                {
                    SetIntegerCell(daysCell, item.DaysWithoutMovement.Value);
                }
                else
                {
                    daysCell.Value = "—";
                    daysCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }

                StyleDataRow(sheet, currentRow, columnCount: 6);
                currentRow++;
            }
        }

        ConfigureSheetLayout(sheet, headerRow: 5, lastTableRow: Math.Max(5, rows.Count + 5), columnWidths: [16, 36, 18, 16, 22, 20]);
        return SaveToBytes(workbook);
    }

    private (string BusinessName, string CurrencyCode) ResolveContext(string? businessName, string? currencyCode)
    {
        var settings = _settingsService?.GetSettings();
        var resolvedBusiness = string.IsNullOrWhiteSpace(businessName)
            ? (string.IsNullOrWhiteSpace(settings?.BusinessName) ? DefaultBusinessName : settings.BusinessName.Trim())
            : businessName.Trim();

        var resolvedCurrency = CurrencyFormatter.NormalizeCurrencyCode(currencyCode ?? settings?.CurrencyCode);
        return (resolvedBusiness, resolvedCurrency);
    }

    private static string GetCurrencyFormat(string currencyCode)
    {
        return currencyCode switch
        {
            "COP" => "\"COP\" $#,##0",
            "EUR" => "€#,##0.00",
            _ => "$#,##0.00"
        };
    }

    private static void ComposeSheetHeader(
        IXLWorksheet sheet,
        string businessName,
        string reportTitle,
        string subtitle,
        int columnCount)
    {
        var businessCell = sheet.Cell(1, 1);
        businessCell.Value = businessName.ToUpperInvariant();
        businessCell.Style.Font.Bold = true;
        businessCell.Style.Font.FontSize = 10;
        businessCell.Style.Font.FontColor = XLColor.FromHtml(BrandAccentHex);
        sheet.Range(1, 1, 1, columnCount).Merge();

        var titleCell = sheet.Cell(2, 1);
        titleCell.Value = reportTitle;
        titleCell.Style.Font.Bold = true;
        titleCell.Style.Font.FontSize = 14;
        titleCell.Style.Font.FontColor = XLColor.FromHtml(TextPrimaryHex);
        sheet.Range(2, 1, 2, columnCount).Merge();

        var subtitleCell = sheet.Cell(3, 1);
        subtitleCell.Value = $"{subtitle} • Generado: {DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}";
        subtitleCell.Style.Font.FontSize = 9.5;
        subtitleCell.Style.Font.FontColor = XLColor.FromHtml(TextMutedHex);
        sheet.Range(3, 1, 3, columnCount).Merge();

        sheet.Row(1).Height = 18;
        sheet.Row(2).Height = 24;
        sheet.Row(3).Height = 18;
        sheet.Row(4).Height = 8;
    }

    private static void WriteTableHeader(
        IXLWorksheet sheet,
        int rowIndex,
        IReadOnlyList<string> headers,
        int[]? rightAlignedColumns = null,
        int[]? centerAlignedColumns = null)
    {
        var rightSet = rightAlignedColumns?.ToHashSet() ?? [];
        var centerSet = centerAlignedColumns?.ToHashSet() ?? [];

        for (var i = 0; i < headers.Count; i++)
        {
            var col = i + 1;
            var cell = sheet.Cell(rowIndex, col);
            cell.Value = headers[i];
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(BrandPrimaryHex);
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 10;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.Horizontal = rightSet.Contains(col)
                ? XLAlignmentHorizontalValues.Right
                : (centerSet.Contains(col) ? XLAlignmentHorizontalValues.Center : XLAlignmentHorizontalValues.Left);
        }

        sheet.Row(rowIndex).Height = 24;
    }

    private static void WriteEmptyNotice(IXLWorksheet sheet, int rowIndex, string message, int columnCount)
    {
        var cell = sheet.Cell(rowIndex, 1);
        cell.Value = message;
        cell.Style.Font.Italic = true;
        cell.Style.Font.FontColor = XLColor.FromHtml(TextMutedHex);
        sheet.Range(rowIndex, 1, rowIndex, columnCount).Merge();
    }

    private static void SetIntegerCell(IXLCell cell, int value)
    {
        cell.Value = value;
        cell.Style.NumberFormat.Format = IntegerFormat;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    private static void SetCurrencyCell(IXLCell cell, decimal value, string numberFormat)
    {
        cell.Value = value;
        cell.Style.NumberFormat.Format = numberFormat;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    private static void StyleDataRow(IXLWorksheet sheet, int rowIndex, int columnCount)
    {
        sheet.Row(rowIndex).Height = 20;
        var rowRange = sheet.Range(rowIndex, 1, rowIndex, columnCount);
        rowRange.Style.Font.FontSize = 10;
        rowRange.Style.Font.FontColor = XLColor.FromHtml(TextPrimaryHex);
        rowRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        rowRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        rowRange.Style.Border.BottomBorderColor = XLColor.FromHtml(BorderLightHex);
    }

    private static void StyleTotalRow(IXLWorksheet sheet, int rowIndex, int columnCount)
    {
        sheet.Row(rowIndex).Height = 22;
        var rowRange = sheet.Range(rowIndex, 1, rowIndex, columnCount);
        rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml(SurfaceSoftHex);
        rowRange.Style.Font.Bold = true;
        rowRange.Style.Font.FontSize = 10;
        rowRange.Style.Font.FontColor = XLColor.FromHtml(TextPrimaryHex);
        rowRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        rowRange.Style.Border.TopBorder = XLBorderStyleValues.Medium;
        rowRange.Style.Border.TopBorderColor = XLColor.FromHtml(BrandPrimaryHex);
        rowRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        rowRange.Style.Border.BottomBorderColor = XLColor.FromHtml(BorderLightHex);
    }

    private static void ApplyStatusColors(IXLCell cell, string statusLabel)
    {
        (string bg, string fg) = statusLabel switch
        {
            "Bajo Stock" => (WarningBgHex, WarningTextHex),
            "Sin Stock" => (DangerBgHex, DangerTextHex),
            _ => (SuccessBgHex, SuccessTextHex)
        };

        cell.Style.Fill.BackgroundColor = XLColor.FromHtml(bg);
        cell.Style.Font.FontColor = XLColor.FromHtml(fg);
    }

    private static void ConfigureSheetLayout(
        IXLWorksheet sheet,
        int headerRow,
        int lastTableRow,
        IReadOnlyList<double> columnWidths)
    {
        for (var i = 0; i < columnWidths.Count; i++)
        {
            sheet.Column(i + 1).Width = columnWidths[i];
        }

        sheet.SheetView.FreezeRows(headerRow);
        sheet.Range(headerRow, 1, lastTableRow, columnWidths.Count).SetAutoFilter();
    }

    private static byte[] SaveToBytes(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

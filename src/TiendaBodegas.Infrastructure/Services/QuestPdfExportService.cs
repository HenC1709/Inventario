using System.Globalization;
using System.Threading;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Application.Services;

namespace TiendaBodegas.Infrastructure.Services;

/// <summary>
/// Generates formatted PDF reports using QuestPDF (Community license) from existing application DTOs and active currency formatting.
/// </summary>
public class QuestPdfExportService : IPdfExportService
{
    private const string DefaultBusinessName = "Almacén Central";
    private const string BrandPrimaryHex = "#1E4E79";
    private const string BrandAccentHex = "#C86D44";
    private const string SurfaceSoftHex = "#F9F7F4";
    private const string BorderLightHex = "#E5DFD7";
    private const string TextPrimaryHex = "#1C1917";
    private const string TextMutedHex = "#78716C";

    private static int _licenseInitialized;
    private readonly ISettingsService? _settingsService;

    /// <summary>
    /// Configures the QuestPDF Community license exactly once per process lifetime.
    /// </summary>
    public static void ConfigureLicenseOnce()
    {
        if (Interlocked.Exchange(ref _licenseInitialized, 1) == 0)
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }
    }

    /// <summary>
    /// Initializes a new instance of <see cref="QuestPdfExportService"/> and ensures the QuestPDF license is configured.
    /// </summary>
    public QuestPdfExportService(ISettingsService? settingsService = null)
    {
        ConfigureLicenseOnce();
        _settingsService = settingsService;
    }

    /// <inheritdoc />
    public byte[] GenerateValuationPdf(
        IEnumerable<CategoryValuationDto> categories,
        string? businessName = null,
        string? currencyCode = null)
    {
        ArgumentNullException.ThrowIfNull(categories);

        var rows = categories.ToList();
        var (resolvedBusiness, resolvedCurrency) = ResolveContext(businessName, currencyCode);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigureStandardPage(page, PageSizes.A4);
                page.Header().Element(h => ComposeHeader(
                    h,
                    resolvedBusiness,
                    "Reporte de Valoración de Inventario por Categoría",
                    $"Moneda activa: {resolvedCurrency}"));

                page.Content().PaddingVertical(12).Column(column =>
                {
                    if (rows.Count == 0)
                    {
                        column.Item().Text("No hay datos de valoración registrados.").FontColor(TextMutedHex).Italic();
                        return;
                    }

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2.2f);
                            columns.ConstantColumn(60);
                            columns.ConstantColumn(70);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                        });

                        table.Header(header =>
                        {
                            StyleHeaderCell(header.Cell()).Text("CATEGORÍA");
                            StyleHeaderCell(header.Cell()).AlignRight().Text("PRODUCTOS");
                            StyleHeaderCell(header.Cell()).AlignRight().Text("STOCK");
                            StyleHeaderCell(header.Cell()).AlignRight().Text($"COSTO ({resolvedCurrency})");
                            StyleHeaderCell(header.Cell()).AlignRight().Text($"VENTA ({resolvedCurrency})");
                            StyleHeaderCell(header.Cell()).AlignRight().Text($"MARGEN ({resolvedCurrency})");
                        });

                        foreach (var row in rows)
                        {
                            StyleBodyCell(table.Cell()).Text(row.Category).SemiBold();
                            StyleBodyCell(table.Cell()).AlignRight().Text(row.ProductCount.ToString(CultureInfo.InvariantCulture));
                            StyleBodyCell(table.Cell()).AlignRight().Text(row.TotalUnits.ToString(CultureInfo.InvariantCulture));
                            StyleBodyCell(table.Cell()).AlignRight().Text(CurrencyFormatter.Format(row.TotalPurchaseCost, resolvedCurrency));
                            StyleBodyCell(table.Cell()).AlignRight().Text(CurrencyFormatter.Format(row.TotalSaleValue, resolvedCurrency));
                            StyleBodyCell(table.Cell()).AlignRight().Text(CurrencyFormatter.Format(row.EstimatedMargin, resolvedCurrency));
                        }

                        StyleSummaryCell(table.Cell()).Text("TOTAL").Bold();
                        StyleSummaryCell(table.Cell()).AlignRight().Text(rows.Sum(r => r.ProductCount).ToString(CultureInfo.InvariantCulture)).Bold();
                        StyleSummaryCell(table.Cell()).AlignRight().Text(rows.Sum(r => r.TotalUnits).ToString(CultureInfo.InvariantCulture)).Bold();
                        StyleSummaryCell(table.Cell()).AlignRight().Text(CurrencyFormatter.Format(rows.Sum(r => r.TotalPurchaseCost), resolvedCurrency)).Bold();
                        StyleSummaryCell(table.Cell()).AlignRight().Text(CurrencyFormatter.Format(rows.Sum(r => r.TotalSaleValue), resolvedCurrency)).Bold();
                        StyleSummaryCell(table.Cell()).AlignRight().Text(CurrencyFormatter.Format(rows.Sum(r => r.EstimatedMargin), resolvedCurrency)).Bold();
                    });
                });

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    /// <inheritdoc />
    public byte[] GenerateInventoryPdf(
        IEnumerable<ProductStockSummaryDto> items,
        string? businessName = null,
        string? currencyCode = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        var rows = items.ToList();
        var (resolvedBusiness, resolvedCurrency) = ResolveContext(businessName, currencyCode);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigureStandardPage(page, PageSizes.A4.Landscape());
                page.Header().Element(h => ComposeHeader(
                    h,
                    resolvedBusiness,
                    "Reporte General de Inventario de Productos",
                    $"Productos listados: {rows.Count} • Moneda activa: {resolvedCurrency}"));

                page.Content().PaddingVertical(12).Column(column =>
                {
                    if (rows.Count == 0)
                    {
                        column.Item().Text("No hay productos registrados para mostrar.").FontColor(TextMutedHex).Italic();
                        return;
                    }

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(80);
                            columns.RelativeColumn(2.4f);
                            columns.RelativeColumn(1.3f);
                            columns.RelativeColumn(1.6f);
                            columns.RelativeColumn(1.3f);
                            columns.RelativeColumn(1.3f);
                            columns.ConstantColumn(55);
                            columns.ConstantColumn(80);
                        });

                        table.Header(header =>
                        {
                            StyleHeaderCell(header.Cell()).Text("SKU");
                            StyleHeaderCell(header.Cell()).Text("PRODUCTO");
                            StyleHeaderCell(header.Cell()).Text("CATEGORÍA");
                            StyleHeaderCell(header.Cell()).Text("PROVEEDOR");
                            StyleHeaderCell(header.Cell()).AlignRight().Text($"COMPRA ({resolvedCurrency})");
                            StyleHeaderCell(header.Cell()).AlignRight().Text($"VENTA ({resolvedCurrency})");
                            StyleHeaderCell(header.Cell()).AlignRight().Text("STOCK");
                            StyleHeaderCell(header.Cell()).AlignCenter().Text("ESTADO");
                        });

                        foreach (var item in rows)
                        {
                            StyleBodyCell(table.Cell()).Text(item.ProductSku);
                            StyleBodyCell(table.Cell()).Text(item.ProductName).SemiBold();
                            StyleBodyCell(table.Cell()).Text(item.Category);
                            StyleBodyCell(table.Cell()).Text(item.SupplierName);
                            StyleBodyCell(table.Cell()).AlignRight().Text(CurrencyFormatter.Format(item.PurchasePrice, resolvedCurrency));
                            StyleBodyCell(table.Cell()).AlignRight().Text(CurrencyFormatter.Format(item.SalePrice, resolvedCurrency));
                            StyleBodyCell(table.Cell()).AlignRight().Text(item.TotalStock.ToString(CultureInfo.InvariantCulture));
                            StyleBodyCell(table.Cell()).AlignCenter().Text(item.StockStatusLabel);
                        }
                    });
                });

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    /// <inheritdoc />
    public byte[] GenerateLowRotationPdf(
        IEnumerable<LowRotationProductDto> items,
        string? businessName = null,
        int auditDays = 30)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (auditDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(auditDays), "El período de auditoría debe ser mayor a cero.");

        var rows = items.ToList();
        var (resolvedBusiness, _) = ResolveContext(businessName, currencyCode: null);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigureStandardPage(page, PageSizes.A4);
                page.Header().Element(h => ComposeHeader(
                    h,
                    resolvedBusiness,
                    $"Auditoría de Productos con Rotación Baja ({auditDays} días)",
                    $"Productos detectados: {rows.Count}"));

                page.Content().PaddingVertical(12).Column(column =>
                {
                    if (rows.Count == 0)
                    {
                        column.Item()
                            .Text($"Sin novedades: todos los productos registraron movimientos en los últimos {auditDays} días.")
                            .FontColor(TextMutedHex);
                        return;
                    }

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(85);
                            columns.RelativeColumn(2.4f);
                            columns.RelativeColumn(1.3f);
                            columns.ConstantColumn(75);
                            columns.ConstantColumn(115);
                            columns.ConstantColumn(85);
                        });

                        table.Header(header =>
                        {
                            StyleHeaderCell(header.Cell()).Text("SKU");
                            StyleHeaderCell(header.Cell()).Text("PRODUCTO");
                            StyleHeaderCell(header.Cell()).Text("CATEGORÍA");
                            StyleHeaderCell(header.Cell()).AlignRight().Text("STOCK");
                            StyleHeaderCell(header.Cell()).AlignCenter().Text("ÚLT. MOVIMIENTO");
                            StyleHeaderCell(header.Cell()).AlignRight().Text("DÍAS SIN ROT.");
                        });

                        foreach (var item in rows)
                        {
                            var lastMovementText = item.LastMovementDate.HasValue
                                ? item.LastMovementDate.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                                : "Sin movimientos";
                            var daysText = item.DaysWithoutMovement.HasValue
                                ? item.DaysWithoutMovement.Value.ToString(CultureInfo.InvariantCulture)
                                : "—";

                            StyleBodyCell(table.Cell()).Text(item.ProductSku);
                            StyleBodyCell(table.Cell()).Text(item.ProductName).SemiBold();
                            StyleBodyCell(table.Cell()).Text(item.Category);
                            StyleBodyCell(table.Cell()).AlignRight().Text(item.CurrentStock.ToString(CultureInfo.InvariantCulture));
                            StyleBodyCell(table.Cell()).AlignCenter().Text(lastMovementText);
                            StyleBodyCell(table.Cell()).AlignRight().Text(daysText);
                        }
                    });
                });

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
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

    private static void ConfigureStandardPage(PageDescriptor page, PageSize size)
    {
        page.Size(size);
        page.Margin(28);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(style => style.FontSize(9.5f).FontColor(TextPrimaryHex));
    }

    private static void ComposeHeader(IContainer container, string businessName, string reportTitle, string subtitle)
    {
        container.BorderBottom(1.5f).BorderColor(BrandAccentHex).PaddingBottom(10).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(businessName.ToUpperInvariant()).FontSize(9).Bold().FontColor(BrandAccentHex);
                column.Item().PaddingTop(2).Text(reportTitle).FontSize(15).Bold().FontColor(TextPrimaryHex);
                column.Item().PaddingTop(2).Text(subtitle).FontSize(9).FontColor(TextMutedHex);
            });

            row.ConstantItem(135).AlignRight().Column(column =>
            {
                column.Item().Text("Generado").FontSize(8).FontColor(TextMutedHex);
                column.Item().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture))
                    .FontSize(9)
                    .SemiBold()
                    .FontColor(TextPrimaryHex);
            });
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.BorderTop(1).BorderColor(BorderLightHex).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text("StockFlow — Documento administrativo generado localmente")
                .FontSize(8)
                .FontColor(TextMutedHex);

            row.ConstantItem(100).AlignRight().Text(text =>
            {
                text.DefaultTextStyle(s => s.FontSize(8).FontColor(TextMutedHex));
                text.Span("Página ");
                text.CurrentPageNumber();
                text.Span(" de ");
                text.TotalPages();
            });
        });
    }

    private static IContainer StyleHeaderCell(IContainer container)
    {
        return container
            .Background(BrandPrimaryHex)
            .PaddingVertical(6)
            .PaddingHorizontal(6)
            .DefaultTextStyle(style => style.FontSize(8.5f).Bold().FontColor(Colors.White));
    }

    private static IContainer StyleBodyCell(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(BorderLightHex)
            .PaddingVertical(5)
            .PaddingHorizontal(6);
    }

    private static IContainer StyleSummaryCell(IContainer container)
    {
        return container
            .Background(SurfaceSoftHex)
            .BorderTop(1.5f)
            .BorderColor(BrandPrimaryHex)
            .PaddingVertical(6)
            .PaddingHorizontal(6);
    }
}

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Application.Services;
using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.UI.Services;

/// <summary>
/// WPF implementation of <see cref="IPrinterService"/> formatting 80 mm thermal receipts via <see cref="FlowDocument"/>
/// and dispatching print jobs through <see cref="PrintDialog"/>.
/// </summary>
public class WpfPrinterService : IPrinterService
{
    /// <summary>
    /// Printable width in WPF DIPs (~73.5 mm usable area on standard 80 mm thermal rolls: 73.55 / 25.4 * 96 ≈ 278 DIPs).
    /// </summary>
    public const double ThermalPrintableWidthDips = 278.0;

    /// <summary>
    /// Minimal page padding in DIPs to maximize usable thermal print head width without clipping.
    /// </summary>
    public static readonly Thickness ThermalPagePadding = new(8, 10, 8, 12);

    private const string DefaultBusinessName = "Almacén Central";
    private const string DividerLine = "----------------------------------------";

    private readonly ISettingsService? _settingsService;
    private readonly Func<FlowDocument, string, bool> _printExecutor;

    /// <summary>
    /// Initializes a new instance of <see cref="WpfPrinterService"/> using the Windows <see cref="PrintDialog"/>.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public WpfPrinterService(ISettingsService? settingsService = null)
        : this(settingsService, DefaultPrintWithDialog)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="WpfPrinterService"/> with a custom print dispatcher for unit testing.
    /// </summary>
    public WpfPrinterService(
        ISettingsService? settingsService,
        Func<FlowDocument, string, bool> printExecutor)
    {
        ArgumentNullException.ThrowIfNull(printExecutor);
        _settingsService = settingsService;
        _printExecutor = printExecutor;
    }

    /// <summary>
    /// Builds a single-column <see cref="FlowDocument"/> sized for 80 mm thermal paper without invoking any Windows dialog.
    /// </summary>
    /// <param name="movement">Enriched stock movement projection to render.</param>
    /// <param name="businessName">Optional store header name; falls back to persisted settings when omitted.</param>
    /// <param name="currencyCode">Optional ISO currency code; falls back to persisted settings when omitted.</param>
    public FlowDocument BuildMovementTicketDocument(
        StockMovementDisplayDto movement,
        string? businessName = null,
        string? currencyCode = null)
    {
        ArgumentNullException.ThrowIfNull(movement);

        var settings = _settingsService?.GetSettings();
        var resolvedBusinessName = string.IsNullOrWhiteSpace(businessName)
            ? (string.IsNullOrWhiteSpace(settings?.BusinessName) ? DefaultBusinessName : settings.BusinessName.Trim())
            : businessName.Trim();

        var resolvedCurrency = CurrencyFormatter.NormalizeCurrencyCode(currencyCode ?? settings?.CurrencyCode);
        var formattedUnitPrice = CurrencyFormatter.Format(movement.UnitPrice, resolvedCurrency);
        var formattedTotal = CurrencyFormatter.Format(movement.TotalAmount, resolvedCurrency);

        var isExit = movement.MovementType == MovementType.Exit;
        var ticketHeader = isExit ? "COMPROBANTE DE VENTA" : "COMPROBANTE DE ENTRADA";
        var folioCode = isExit ? $"#V-{movement.Id:0000}" : $"#C-{movement.Id:0000}";

        var document = new FlowDocument
        {
            PageWidth = ThermalPrintableWidthDips,
            PagePadding = ThermalPagePadding,
            ColumnWidth = ThermalPrintableWidthDips,
            ColumnGap = 0,
            FontFamily = new FontFamily("Consolas, Segoe UI"),
            FontSize = 10.5,
            Foreground = Brushes.Black,
            Background = Brushes.White
        };

        document.Blocks.Add(new Paragraph(new Run(resolvedBusinessName))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 2)
        });

        document.Blocks.Add(new Paragraph
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 9.5,
            Margin = new Thickness(0, 0, 0, 4),
            Inlines =
            {
                new Run(ticketHeader) { FontWeight = FontWeights.SemiBold },
                new LineBreak(),
                new Run($"Folio: {folioCode}"),
                new LineBreak(),
                new Run($"Fecha: {movement.Date.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}")
            }
        });

        document.Blocks.Add(CreateDividerParagraph());

        document.Blocks.Add(new Paragraph
        {
            Margin = new Thickness(0, 2, 0, 2),
            Inlines =
            {
                new Run(movement.ProductName) { FontWeight = FontWeights.Bold },
                new LineBreak(),
                new Run($"SKU: {movement.ProductSku} | Tipo: {movement.MovementTypeLabel}") { FontSize = 9.5 }
            }
        });

        var contentWidth = ThermalPrintableWidthDips - ThermalPagePadding.Left - ThermalPagePadding.Right;
        var lineTable = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 2, 0, 4)
        };
        lineTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.58) });
        lineTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.42) });

        var rowGroup = new TableRowGroup();
        var detailRow = new TableRow();
        detailRow.Cells.Add(new TableCell(new Paragraph(new Run($"{movement.Quantity} x {formattedUnitPrice}"))
        {
            Margin = new Thickness(0),
            FontSize = 10
        }));
        detailRow.Cells.Add(new TableCell(new Paragraph(new Run(formattedTotal))
        {
            Margin = new Thickness(0),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Right
        }));
        rowGroup.Rows.Add(detailRow);
        lineTable.RowGroups.Add(rowGroup);
        document.Blocks.Add(lineTable);

        if (!string.IsNullOrWhiteSpace(movement.Notes))
        {
            document.Blocks.Add(new Paragraph(new Run($"Obs.: {movement.Notes.Trim()}"))
            {
                FontSize = 9.5,
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        document.Blocks.Add(CreateDividerParagraph());

        document.Blocks.Add(new Paragraph(new Run($"TOTAL ({resolvedCurrency}): {formattedTotal}"))
        {
            TextAlignment = TextAlignment.Right,
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 2, 0, 6)
        });

        document.Blocks.Add(new Paragraph(new Run("Documento generado localmente"))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 8.5,
            Margin = new Thickness(0)
        });

        return document;
    }

    /// <inheritdoc />
    public bool PrintMovementTicket(
        StockMovementDisplayDto movement,
        string? businessName = null,
        string? currencyCode = null)
    {
        var document = BuildMovementTicketDocument(movement, businessName, currencyCode);
        var jobDescription = movement.MovementType == MovementType.Exit
            ? $"Ticket Venta V-{movement.Id:0000}"
            : $"Ticket Entrada C-{movement.Id:0000}";

        return _printExecutor(document, jobDescription);
    }

    private static Paragraph CreateDividerParagraph()
    {
        return new Paragraph(new Run(DividerLine))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 8.5,
            Margin = new Thickness(0, 2, 0, 2)
        };
    }

    private static bool DefaultPrintWithDialog(FlowDocument document, string description)
    {
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true)
            return false;

        IDocumentPaginatorSource paginatorSource = document;
        printDialog.PrintDocument(paginatorSource.DocumentPaginator, description);
        return true;
    }
}

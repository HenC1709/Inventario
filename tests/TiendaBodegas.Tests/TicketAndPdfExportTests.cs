using System.IO;
using System.Text;
using System.Windows.Documents;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Enums;
using TiendaBodegas.UI;
using TiendaBodegas.UI.Services;
using TiendaBodegas.UI.ViewModels;
using Xunit;

namespace TiendaBodegas.Tests;

/// <summary>
/// Unit and integration tests covering N1 (80 mm thermal <see cref="FlowDocument"/> receipts) and N2 (QuestPDF exports).
/// </summary>
public class TicketAndPdfExportTests : IDisposable
{
    private readonly string _tempDirectory;

    /// <summary>
    /// Initializes an isolated temporary directory for each test execution.
    /// </summary>
    public TicketAndPdfExportTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "TiendaBodegasN1N2Tests_" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Verifies that <see cref="WpfPrinterService.BuildMovementTicketDocument"/> produces a single-column
    /// 80 mm thermal <see cref="FlowDocument"/> (~278 DIPs, minimal padding, <c>ColumnWidth == PageWidth</c>)
    /// with formatted active currency values without opening any dialog.
    /// </summary>
    [Fact]
    public void WpfPrinterService_BuildMovementTicketDocument_Configures80MmLayoutAndCurrencyContent()
    {
        RunOnStaThread(() =>
        {
            var printerService = new WpfPrinterService();

            var saleMovement = new StockMovementDisplayDto(
                Id: 7,
                Date: new DateTime(2026, 11, 23, 18, 45, 0),
                MovementType: MovementType.Exit,
                MovementTypeLabel: "Salida",
                ProductId: 1,
                ProductSku: "SKU-10482",
                ProductName: "Café Espresso Molido 1kg",
                ProductDisplay: "#1 — Café Espresso Molido 1kg",
                Quantity: 5,
                UnitPrice: 48000m,
                TotalAmount: 240000m,
                Notes: "Venta mostrador");

            var saleDoc = printerService.BuildMovementTicketDocument(
                saleMovement,
                businessName: "Bodega Central Norte",
                currencyCode: "COP");

            Assert.InRange(saleDoc.PageWidth, 275.0, 280.0);
            Assert.Equal(saleDoc.PageWidth, saleDoc.ColumnWidth);
            Assert.Equal(0.0, saleDoc.ColumnGap);
            Assert.True(saleDoc.PagePadding.Left <= 12.0 && saleDoc.PagePadding.Right <= 12.0);
            Assert.NotEmpty(saleDoc.Blocks);

            var saleText = new TextRange(saleDoc.ContentStart, saleDoc.ContentEnd).Text;
            Assert.Contains("Bodega Central Norte", saleText);
            Assert.Contains("COMPROBANTE DE VENTA", saleText);
            Assert.Contains("#V-0007", saleText);
            Assert.Contains("Café Espresso Molido 1kg", saleText);
            Assert.Contains("SKU-10482", saleText);
            Assert.Contains("5 x COP $48,000", saleText);
            Assert.Contains("TOTAL (COP): COP $240,000", saleText);
            Assert.Contains("Venta mostrador", saleText);

            var entryMovement = new StockMovementDisplayDto(
                Id: 2,
                Date: new DateTime(2026, 11, 23, 9, 15, 0),
                MovementType: MovementType.Entry,
                MovementTypeLabel: "Entrada",
                ProductId: 2,
                ProductSku: "SKU-20948",
                ProductName: "Aceite de Oliva Extra Virgen",
                ProductDisplay: "#2 — Aceite de Oliva Extra Virgen",
                Quantity: 2,
                UnitPrice: 12.50m,
                TotalAmount: 25.00m,
                Notes: string.Empty);

            var entryDoc = printerService.BuildMovementTicketDocument(entryMovement, "Almacén Europeo", "EUR");
            var entryText = new TextRange(entryDoc.ContentStart, entryDoc.ContentEnd).Text;
            Assert.Contains("COMPROBANTE DE ENTRADA", entryText);
            Assert.Contains("#C-0002", entryText);
            Assert.Contains("€25.00", entryText);

            Assert.Throws<ArgumentNullException>(() => printerService.BuildMovementTicketDocument(null!));
        });
    }

    /// <summary>
    /// Verifies that <see cref="WpfPrinterService.PrintMovementTicket"/> reads persisted store settings when omitted
    /// and dispatches through its injectable print executor without opening a Windows <c>PrintDialog</c>.
    /// </summary>
    [Fact]
    public void WpfPrinterService_PrintMovementTicket_UsesSettingsAndDispatchesWithoutDialog()
    {
        using var provider = BuildSeededProvider();
        var settingsService = provider.GetRequiredService<ISettingsService>();
        settingsService.UpdateSettings("Almacén Andino", "COP", 10);

        RunOnStaThread(() =>
        {
            FlowDocument? capturedDocument = null;
            string? capturedJobTitle = null;

            var acceptedPrinter = new WpfPrinterService(settingsService, (doc, title) =>
            {
                capturedDocument = doc;
                capturedJobTitle = title;
                return true;
            });

            var movement = new StockMovementDisplayDto(
                14, DateTime.Now, MovementType.Exit, "Salida", 1, "CAF-500", "Café Excelso", "#1 — Café Excelso",
                3, 25000m, 75000m, "Cliente frecuente");

            Assert.True(acceptedPrinter.PrintMovementTicket(movement));
            Assert.NotNull(capturedDocument);
            Assert.Equal("Ticket Venta V-0014", capturedJobTitle);

            var renderedText = new TextRange(capturedDocument!.ContentStart, capturedDocument.ContentEnd).Text;
            Assert.Contains("Almacén Andino", renderedText);
            Assert.Contains("COP $75,000", renderedText);

            var cancelledPrinter = new WpfPrinterService(settingsService, (_, _) => false);
            Assert.False(cancelledPrinter.PrintMovementTicket(movement));
        });
    }

    /// <summary>
    /// Verifies that DI configures <see cref="LicenseType.Community"/> and <see cref="IPdfExportService"/> generates
    /// non-empty valid PDF binaries starting with <c>%PDF</c> across valuation, inventory, and low-rotation reports.
    /// </summary>
    [Fact]
    public void PdfExportService_ConfiguresCommunityLicense_AndGeneratesValidPdfFilesWithHeader()
    {
        using var provider = BuildSeededProvider();
        Assert.Equal(LicenseType.Community, QuestPDF.Settings.License);

        var inventoryService = provider.GetRequiredService<IInventoryService>();
        var pdfService = provider.GetRequiredService<IPdfExportService>();

        var valuationPdf = pdfService.GenerateValuationPdf(
            inventoryService.GetCategoryValuationReport(),
            businessName: "Almacén Central",
            currencyCode: "COP");
        AssertValidPdfBytes(valuationPdf);

        var inventoryPdf = pdfService.GenerateInventoryPdf(
            inventoryService.GetProductStockSummary(),
            businessName: "Almacén Central",
            currencyCode: "EUR");
        AssertValidPdfBytes(inventoryPdf);

        var lowRotationPdf = pdfService.GenerateLowRotationPdf(
            inventoryService.GetLowRotationProducts(30),
            businessName: "Almacén Central",
            auditDays: 30);
        AssertValidPdfBytes(lowRotationPdf);

        var emptyValuationPdf = pdfService.GenerateValuationPdf(Array.Empty<CategoryValuationDto>(), "Almacén Vacío", "USD");
        AssertValidPdfBytes(emptyValuationPdf);
    }

    /// <summary>
    /// Verifies PDF export commands on <see cref="ReportsViewModel"/> and ticket print commands on <see cref="MovementsViewModel"/>.
    /// </summary>
    [Fact]
    public void ViewModels_ExposeCleanPdfExportAndTicketPrintCommands()
    {
        Directory.CreateDirectory(_tempDirectory);
        using var provider = BuildSeededProvider();

        var inventoryService = provider.GetRequiredService<IInventoryService>();
        var catalogService = provider.GetRequiredService<ICatalogService>();
        var csvService = provider.GetRequiredService<IInventoryExportService>();
        var pdfService = provider.GetRequiredService<IPdfExportService>();
        var settingsService = provider.GetRequiredService<ISettingsService>();

        var dialogStub = new StubDialogService();
        var printerStub = new StubPrinterService { NextResult = true };

        var reportsVm = new ReportsViewModel(
            inventoryService,
            csvService,
            dialogStub,
            pdfService,
            printerStub,
            settingsService);

        var valuationPdfPath = Path.Combine(_tempDirectory, "valoracion.pdf");
        dialogStub.NextSavePath = valuationPdfPath;
        reportsVm.ExportValuationPdfCommand.Execute(null);
        Assert.False(reportsVm.IsErrorMessage);
        Assert.True(File.Exists(valuationPdfPath));
        AssertValidPdfBytes(File.ReadAllBytes(valuationPdfPath));

        var auditPdfPath = Path.Combine(_tempDirectory, "rotacion_baja.pdf");
        dialogStub.NextSavePath = auditPdfPath;
        reportsVm.ExportLowRotationPdfCommand.Execute(null);
        Assert.False(reportsVm.IsErrorMessage);
        Assert.True(File.Exists(auditPdfPath));
        AssertValidPdfBytes(File.ReadAllBytes(auditPdfPath));

        var inventoryPdfPath = Path.Combine(_tempDirectory, "inventario.pdf");
        dialogStub.NextSavePath = inventoryPdfPath;
        reportsVm.ExportInventoryPdfCommand.Execute(null);
        Assert.False(reportsVm.IsErrorMessage);
        Assert.True(File.Exists(inventoryPdfPath));
        AssertValidPdfBytes(File.ReadAllBytes(inventoryPdfPath));

        reportsVm.PrintLatestMovementTicketCommand.Execute(null);
        Assert.False(reportsVm.IsErrorMessage);
        Assert.Equal(1, printerStub.PrintCalls);

        var movementsVm = new MovementsViewModel(
            inventoryService,
            catalogService,
            dialogStub,
            settingsService,
            printerStub);

        var firstMovement = movementsVm.Movements.First();
        movementsVm.PrintMovementTicketCommand.Execute(firstMovement);
        Assert.False(movementsVm.IsErrorMessage);
        Assert.Equal(2, printerStub.PrintCalls);
        Assert.Equal(firstMovement.Id, printerStub.LastPrintedMovement?.Id);
    }

    private static void AssertValidPdfBytes(byte[] pdfBytes)
    {
        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 100, "Expected non-empty PDF binary payload.");
        var magicHeader = Encoding.ASCII.GetString(pdfBytes, 0, 4);
        Assert.Equal("%PDF", magicHeader);
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured != null)
            throw captured;
    }

    private ServiceProvider BuildSeededProvider()
    {
        var services = new ServiceCollection();
        services.AddTiendaBodegasServices(_tempDirectory);
        var provider = services.BuildServiceProvider();
        DependencyInjection.InitializeDatabase(provider);
        return provider;
    }

    /// <summary>
    /// Cleans up the temporary directory after each test.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    private sealed class StubDialogService : IDialogService
    {
        public string? NextSavePath { get; set; }
        public bool ShowProductForm(ProductFormViewModel viewModel) => false;
        public bool ShowSupplierForm(SupplierFormViewModel viewModel) => false;
        public bool ShowMovementForm(MovementFormViewModel viewModel) => false;
        public bool Confirm(string message, string title) => false;
        public string? ShowSaveFileDialog(string defaultFileName, string filter) => NextSavePath;
    }

    private sealed class StubPrinterService : IPrinterService
    {
        public bool NextResult { get; set; }
        public int PrintCalls { get; private set; }
        public StockMovementDisplayDto? LastPrintedMovement { get; private set; }

        public bool PrintMovementTicket(StockMovementDisplayDto movement, string? businessName = null, string? currencyCode = null)
        {
            PrintCalls++;
            LastPrintedMovement = movement;
            return NextResult;
        }
    }
}

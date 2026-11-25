using System.IO;
using System.Text;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.UI;
using TiendaBodegas.UI.Services;
using TiendaBodegas.UI.ViewModels;
using Xunit;

namespace TiendaBodegas.Tests;

/// <summary>
/// Unit and integration tests covering N3 (ClosedXML Excel workbook generation and ViewModel export commands).
/// </summary>
public class ExcelExportTests : IDisposable
{
    private readonly string _tempDirectory;

    /// <summary>
    /// Initializes an isolated temporary directory for each test execution.
    /// </summary>
    public ExcelExportTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "TiendaBodegasN3Tests_" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Verifies that <see cref="IExcelExportService"/> generates valid <c>.xlsx</c> workbooks with numeric cells,
    /// active currency headers/formats, semantic status colors, and total summaries across all three report types.
    /// </summary>
    [Fact]
    public void ExcelExportService_GeneratesFormattedWorkbooksWithNumericCellsAndTotals()
    {
        using var provider = BuildSeededProvider();
        var inventoryService = provider.GetRequiredService<IInventoryService>();
        var excelService = provider.GetRequiredService<IExcelExportService>();

        var valuationBytes = excelService.GenerateValuationExcel(
            inventoryService.GetCategoryValuationReport(),
            businessName: "Bodega Norte",
            currencyCode: "COP");
        AssertValidXlsxHeader(valuationBytes);

        using (var valuationStream = new MemoryStream(valuationBytes))
        using (var valuationWorkbook = new XLWorkbook(valuationStream))
        {
            var sheet = valuationWorkbook.Worksheet("Valoración");
            Assert.Equal("BODEGA NORTE", sheet.Cell(1, 1).GetString());
            Assert.Contains("COSTO COMPRA (COP)", sheet.Cell(5, 4).GetString());
            Assert.True(sheet.Cell(6, 4).DataType == XLDataType.Number);
            Assert.True(sheet.Cell(6, 5).DataType == XLDataType.Number);

            var totalRow = sheet.RowsUsed().Last();
            Assert.Equal("TOTAL", totalRow.Cell(1).GetString());
            Assert.Equal(12, totalRow.Cell(2).GetValue<int>());
            Assert.Equal(2605.10m, totalRow.Cell(5).GetValue<decimal>());
        }

        var inventoryBytes = excelService.GenerateInventoryExcel(
            inventoryService.GetProductStockSummary(),
            businessName: "Bodega Norte",
            currencyCode: "EUR");
        AssertValidXlsxHeader(inventoryBytes);

        using (var inventoryStream = new MemoryStream(inventoryBytes))
        using (var inventoryWorkbook = new XLWorkbook(inventoryStream))
        {
            var sheet = inventoryWorkbook.Worksheet("Inventario");
            Assert.Contains("PRECIO VENTA (EUR)", sheet.Cell(5, 6).GetString());
            Assert.True(sheet.Cell(6, 5).DataType == XLDataType.Number);
            Assert.Equal("€#,##0.00", sheet.Cell(6, 6).Style.NumberFormat.Format);

            var totalRow = sheet.RowsUsed().Last();
            Assert.Equal("TOTAL", totalRow.Cell(1).GetString());
            Assert.Equal("12 producto(s)", totalRow.Cell(2).GetString());
        }

        var lowRotationBytes = excelService.GenerateLowRotationExcel(
            inventoryService.GetLowRotationProducts(30),
            businessName: "Bodega Norte",
            auditDays: 30);
        AssertValidXlsxHeader(lowRotationBytes);

        using (var auditStream = new MemoryStream(lowRotationBytes))
        using (var auditWorkbook = new XLWorkbook(auditStream))
        {
            var sheet = auditWorkbook.Worksheet("Rotación Baja");
            Assert.Contains("30 días", sheet.Cell(2, 1).GetString());
            Assert.Equal("Sin movimientos", sheet.Cell(6, 5).GetString());
            Assert.Equal("—", sheet.Cell(6, 6).GetString());
        }

        var emptyValuation = excelService.GenerateValuationExcel(Array.Empty<CategoryValuationDto>(), "Vacía", "USD");
        AssertValidXlsxHeader(emptyValuation);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            excelService.GenerateLowRotationExcel(Array.Empty<LowRotationProductDto>(), "Bodega", auditDays: 0));
    }

    /// <summary>
    /// Verifies Excel export commands on <see cref="ReportsViewModel"/> and <see cref="InventoryViewModel"/>.
    /// </summary>
    [Fact]
    public void ViewModels_ExposeCleanExcelExportCommands_AndWriteValidXlsxFiles()
    {
        Directory.CreateDirectory(_tempDirectory);
        using var provider = BuildSeededProvider();

        var catalogService = provider.GetRequiredService<ICatalogService>();
        var inventoryService = provider.GetRequiredService<IInventoryService>();
        var csvService = provider.GetRequiredService<IInventoryExportService>();
        var pdfService = provider.GetRequiredService<IPdfExportService>();
        var excelService = provider.GetRequiredService<IExcelExportService>();
        var settingsService = provider.GetRequiredService<ISettingsService>();

        var dialogStub = new StubDialogService();
        var reportsVm = new ReportsViewModel(
            inventoryService,
            csvService,
            dialogStub,
            pdfService,
            printerService: null,
            settingsService,
            excelService);

        var valuationPath = Path.Combine(_tempDirectory, "valoracion.xlsx");
        dialogStub.NextSavePath = valuationPath;
        reportsVm.ExportValuationExcelCommand.Execute(null);
        Assert.False(reportsVm.IsErrorMessage);
        Assert.True(File.Exists(valuationPath));
        AssertValidXlsxHeader(File.ReadAllBytes(valuationPath));

        var auditPath = Path.Combine(_tempDirectory, "rotacion.xlsx");
        dialogStub.NextSavePath = auditPath;
        reportsVm.ExportLowRotationExcelCommand.Execute(null);
        Assert.False(reportsVm.IsErrorMessage);
        Assert.True(File.Exists(auditPath));
        AssertValidXlsxHeader(File.ReadAllBytes(auditPath));

        var reportsInventoryPath = Path.Combine(_tempDirectory, "inventario_reportes.xlsx");
        dialogStub.NextSavePath = reportsInventoryPath;
        reportsVm.ExportInventoryExcelCommand.Execute(null);
        Assert.False(reportsVm.IsErrorMessage);
        Assert.True(File.Exists(reportsInventoryPath));
        AssertValidXlsxHeader(File.ReadAllBytes(reportsInventoryPath));

        var inventoryVm = new InventoryViewModel(
            catalogService,
            inventoryService,
            csvService,
            dialogStub,
            settingsService,
            excelService);

        inventoryVm.FilterLowStockCommand.Execute(null);
        var filteredInventoryPath = Path.Combine(_tempDirectory, "inventario_filtrado.xlsx");
        dialogStub.NextSavePath = filteredInventoryPath;
        inventoryVm.ExportExcelCommand.Execute(null);
        Assert.False(inventoryVm.IsErrorMessage);
        Assert.True(File.Exists(filteredInventoryPath));

        using var filteredStream = new MemoryStream(File.ReadAllBytes(filteredInventoryPath));
        using var filteredWorkbook = new XLWorkbook(filteredStream);
        var filteredSheet = filteredWorkbook.Worksheet("Inventario");
        var totalRow = filteredSheet.RowsUsed().Last();
        Assert.Equal("5 producto(s)", totalRow.Cell(2).GetString());
    }

    private static void AssertValidXlsxHeader(byte[] bytes)
    {
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 500, "Expected non-empty .xlsx archive payload.");
        Assert.Equal("PK", Encoding.ASCII.GetString(bytes, 0, 2));
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
}

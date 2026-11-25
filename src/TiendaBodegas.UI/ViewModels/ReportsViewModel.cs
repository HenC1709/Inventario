using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.UI.Services;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// ViewModel coordinating inventory valuation CSV downloads and 30-day low-rotation audit generation.
/// </summary>
public partial class ReportsViewModel : ObservableObject
{
    public const int DefaultAuditDays = 30;

    private readonly IInventoryService _inventoryService;
    private readonly IInventoryExportService _exportService;
    private readonly IDialogService _dialogService;
    private readonly IPdfExportService? _pdfExportService;
    private readonly IPrinterService? _printerService;
    private readonly ISettingsService? _settingsService;
    private readonly IExcelExportService? _excelExportService;

    [ObservableProperty]
    private bool _hasGeneratedAudit;

    [ObservableProperty]
    private string _auditSummaryText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isErrorMessage;

    /// <summary>
    /// Gets the collection of low-rotation products produced by the latest audit query.
    /// </summary>
    public ObservableCollection<LowRotationProductDto> LowRotationItems { get; } = new();

    /// <summary>
    /// Gets a value indicating whether the generated low-rotation audit returned zero items.
    /// </summary>
    public bool HasNoLowRotationItems => LowRotationItems.Count == 0;

    /// <summary>
    /// Gets the page title displayed in the reports header.
    /// </summary>
    public string Title => "Reportes";

    /// <summary>
    /// Gets the subtitle describing the reports section.
    /// </summary>
    public string Subtitle => "Generación de documentos y auditorías de stock";

    /// <summary>
    /// Initializes a new instance of <see cref="ReportsViewModel"/>.
    /// </summary>
    public ReportsViewModel(
        IInventoryService inventoryService,
        IInventoryExportService exportService,
        IDialogService dialogService,
        IPdfExportService? pdfExportService = null,
        IPrinterService? printerService = null,
        ISettingsService? settingsService = null,
        IExcelExportService? excelExportService = null)
    {
        _inventoryService = inventoryService;
        _exportService = exportService;
        _dialogService = dialogService;
        _pdfExportService = pdfExportService;
        _printerService = printerService;
        _settingsService = settingsService;
        _excelExportService = excelExportService;
    }

    /// <summary>
    /// Clears stale feedback banners and refreshes the audit table if it was already generated.
    /// </summary>
    public void LoadData()
    {
        ClearStatus();
        if (HasGeneratedAudit)
            PopulateLowRotationAudit();
    }

    /// <summary>
    /// Generates a category valuation report and saves it to a user-selected CSV file.
    /// </summary>
    [RelayCommand]
    public void ExportValuationCsv()
    {
        ClearStatus();
        var report = _inventoryService.GetCategoryValuationReport().ToList();
        if (report.Count == 0)
        {
            SetErrorStatus("No hay productos registrados para generar la valoración.");
            return;
        }

        var path = _dialogService.ShowSaveFileDialog(
            "valoracion_inventario.csv",
            "Archivo CSV (*.csv)|*.csv");

        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var csv = _exportService.GenerateValuationCsv(report);
            File.WriteAllText(path, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            SetSuccessStatus("Reporte de valoración CSV exportado correctamente.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetErrorStatus($"No se pudo exportar la valoración: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs the 30-day low-rotation audit and displays the results preview in the UI.
    /// </summary>
    [RelayCommand]
    public void GenerateLowRotationReport()
    {
        ClearStatus();
        PopulateLowRotationAudit();
        HasGeneratedAudit = true;
        SetSuccessStatus("Auditoría de rotación baja generada correctamente.");
    }

    /// <summary>
    /// Exports the low-rotation product audit to a user-selected CSV file.
    /// </summary>
    [RelayCommand]
    public void ExportLowRotationCsv()
    {
        ClearStatus();
        if (!HasGeneratedAudit)
            PopulateLowRotationAudit();

        var path = _dialogService.ShowSaveFileDialog(
            "productos_rotacion_baja.csv",
            "Archivo CSV (*.csv)|*.csv");

        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var csv = _exportService.GenerateLowRotationCsv(LowRotationItems);
            File.WriteAllText(path, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            SetSuccessStatus("Auditoría de rotación baja exportada a CSV correctamente.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetErrorStatus($"No se pudo exportar la auditoría: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a category valuation report in PDF format using the active currency and saves it to a user-selected file.
    /// </summary>
    [RelayCommand]
    public void ExportValuationPdf()
    {
        ClearStatus();
        if (_pdfExportService == null)
        {
            SetErrorStatus("El servicio de exportación PDF no está disponible.");
            return;
        }

        var report = _inventoryService.GetCategoryValuationReport().ToList();
        if (report.Count == 0)
        {
            SetErrorStatus("No hay productos registrados para generar la valoración.");
            return;
        }

        var path = _dialogService.ShowSaveFileDialog(
            "valoracion_inventario.pdf",
            "Documento PDF (*.pdf)|*.pdf");

        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var settings = _settingsService?.GetSettings();
            var pdfBytes = _pdfExportService.GenerateValuationPdf(report, settings?.BusinessName, settings?.CurrencyCode);
            File.WriteAllBytes(path, pdfBytes);
            SetSuccessStatus("Reporte de valoración PDF exportado correctamente.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetErrorStatus($"No se pudo exportar el PDF de valoración: {ex.Message}");
        }
    }

    /// <summary>
    /// Exports the 30-day low-rotation product audit to a formatted PDF document.
    /// </summary>
    [RelayCommand]
    public void ExportLowRotationPdf()
    {
        ClearStatus();
        if (_pdfExportService == null)
        {
            SetErrorStatus("El servicio de exportación PDF no está disponible.");
            return;
        }

        if (!HasGeneratedAudit)
            PopulateLowRotationAudit();

        var path = _dialogService.ShowSaveFileDialog(
            "productos_rotacion_baja.pdf",
            "Documento PDF (*.pdf)|*.pdf");

        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var settings = _settingsService?.GetSettings();
            var pdfBytes = _pdfExportService.GenerateLowRotationPdf(LowRotationItems, settings?.BusinessName, DefaultAuditDays);
            File.WriteAllBytes(path, pdfBytes);
            SetSuccessStatus("Auditoría de rotación baja exportada a PDF correctamente.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetErrorStatus($"No se pudo exportar el PDF de auditoría: {ex.Message}");
        }
    }

    /// <summary>
    /// Exports the full product inventory summary to a formatted PDF document.
    /// </summary>
    [RelayCommand]
    public void ExportInventoryPdf()
    {
        ClearStatus();
        if (_pdfExportService == null)
        {
            SetErrorStatus("El servicio de exportación PDF no está disponible.");
            return;
        }

        var items = _inventoryService.GetProductStockSummary().ToList();
        if (items.Count == 0)
        {
            SetErrorStatus("No hay productos registrados para exportar.");
            return;
        }

        var path = _dialogService.ShowSaveFileDialog(
            "inventario_productos.pdf",
            "Documento PDF (*.pdf)|*.pdf");

        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var settings = _settingsService?.GetSettings();
            var pdfBytes = _pdfExportService.GenerateInventoryPdf(items, settings?.BusinessName, settings?.CurrencyCode);
            File.WriteAllBytes(path, pdfBytes);
            SetSuccessStatus("Reporte de inventario PDF exportado correctamente.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetErrorStatus($"No se pudo exportar el PDF de inventario: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a formatted Excel workbook for category valuation using the active currency.
    /// </summary>
    [RelayCommand]
    public void ExportValuationExcel()
    {
        ClearStatus();
        if (_excelExportService == null)
        {
            SetErrorStatus("El servicio de exportación Excel no está disponible.");
            return;
        }

        var report = _inventoryService.GetCategoryValuationReport().ToList();
        if (report.Count == 0)
        {
            SetErrorStatus("No hay productos registrados para generar la valoración.");
            return;
        }

        TrySaveBinaryReport(
            "valoracion_inventario.xlsx",
            "Libro de Excel (*.xlsx)|*.xlsx",
            () =>
            {
                var settings = _settingsService?.GetSettings();
                return _excelExportService.GenerateValuationExcel(report, settings?.BusinessName, settings?.CurrencyCode);
            },
            "Reporte de valoración Excel exportado correctamente.",
            "No se pudo exportar el Excel de valoración");
    }

    /// <summary>
    /// Exports the 30-day low-rotation product audit to a formatted Excel workbook.
    /// </summary>
    [RelayCommand]
    public void ExportLowRotationExcel()
    {
        ClearStatus();
        if (_excelExportService == null)
        {
            SetErrorStatus("El servicio de exportación Excel no está disponible.");
            return;
        }

        if (!HasGeneratedAudit)
            PopulateLowRotationAudit();

        TrySaveBinaryReport(
            "productos_rotacion_baja.xlsx",
            "Libro de Excel (*.xlsx)|*.xlsx",
            () =>
            {
                var settings = _settingsService?.GetSettings();
                return _excelExportService.GenerateLowRotationExcel(LowRotationItems, settings?.BusinessName, DefaultAuditDays);
            },
            "Auditoría de rotación baja exportada a Excel correctamente.",
            "No se pudo exportar el Excel de auditoría");
    }

    /// <summary>
    /// Exports the full product inventory summary to a formatted Excel workbook.
    /// </summary>
    [RelayCommand]
    public void ExportInventoryExcel()
    {
        ClearStatus();
        if (_excelExportService == null)
        {
            SetErrorStatus("El servicio de exportación Excel no está disponible.");
            return;
        }

        var items = _inventoryService.GetProductStockSummary().ToList();
        if (items.Count == 0)
        {
            SetErrorStatus("No hay productos registrados para exportar.");
            return;
        }

        TrySaveBinaryReport(
            "inventario_productos.xlsx",
            "Libro de Excel (*.xlsx)|*.xlsx",
            () =>
            {
                var settings = _settingsService?.GetSettings();
                return _excelExportService.GenerateInventoryExcel(items, settings?.BusinessName, settings?.CurrencyCode);
            },
            "Reporte de inventario Excel exportado correctamente.",
            "No se pudo exportar el Excel de inventario");
    }

    /// <summary>
    /// Prints an 80 mm thermal receipt for the most recent inventory movement.
    /// </summary>
    [RelayCommand]
    public void PrintLatestMovementTicket()
    {
        ClearStatus();
        if (_printerService == null)
        {
            SetErrorStatus("El servicio de impresión no está disponible.");
            return;
        }

        var latestMovement = _inventoryService.GetMovementDetails().FirstOrDefault();
        if (latestMovement == null)
        {
            SetErrorStatus("No hay movimientos registrados para imprimir.");
            return;
        }

        var settings = _settingsService?.GetSettings();
        if (_printerService.PrintMovementTicket(latestMovement, settings?.BusinessName, settings?.CurrencyCode))
        {
            SetSuccessStatus("Ticket 80 mm enviado a impresión correctamente.");
        }
    }

    private void PopulateLowRotationAudit()
    {
        var items = _inventoryService.GetLowRotationProducts(DefaultAuditDays).ToList();

        LowRotationItems.Clear();
        foreach (var item in items)
        {
            LowRotationItems.Add(item);
        }

        AuditSummaryText = LowRotationItems.Count == 0
            ? $"Sin novedades: todos los productos registraron movimientos en los últimos {DefaultAuditDays} días."
            : $"{LowRotationItems.Count} producto(s) sin movimientos en los últimos {DefaultAuditDays} días.";

        OnPropertyChanged(nameof(HasNoLowRotationItems));
    }

    private void TrySaveBinaryReport(
        string defaultFileName,
        string filter,
        Func<byte[]> generateBytes,
        string successMessage,
        string errorPrefix)
    {
        var path = _dialogService.ShowSaveFileDialog(defaultFileName, filter);
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            File.WriteAllBytes(path, generateBytes());
            SetSuccessStatus(successMessage);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetErrorStatus($"{errorPrefix}: {ex.Message}");
        }
    }

    private void ClearStatus()
    {
        StatusMessage = string.Empty;
        IsErrorMessage = false;
    }

    private void SetSuccessStatus(string message)
    {
        IsErrorMessage = false;
        StatusMessage = message;
    }

    private void SetErrorStatus(string message)
    {
        IsErrorMessage = true;
        StatusMessage = message;
    }
}

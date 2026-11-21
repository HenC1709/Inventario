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
        IDialogService dialogService)
    {
        _inventoryService = inventoryService;
        _exportService = exportService;
        _dialogService = dialogService;
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

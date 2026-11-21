using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiendaBodegas.Application.Interfaces;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// Display option pairing an ISO currency code with its human-readable label.
/// </summary>
public sealed record CurrencyOption(string Code, string Label);

/// <summary>
/// ViewModel managing persisted application preferences with <see cref="System.ComponentModel.INotifyDataErrorInfo"/> validation.
/// </summary>
public partial class SettingsViewModel : ValidatableViewModel
{
    private readonly ISettingsService _settingsService;
    private bool _isLoading;

    [ObservableProperty]
    private string _businessName = string.Empty;

    [ObservableProperty]
    private string _selectedCurrencyCode = "USD";

    [ObservableProperty]
    private string _lowStockThresholdText = "10";

    [ObservableProperty]
    private string _businessNameError = string.Empty;

    [ObservableProperty]
    private string _lowStockThresholdError = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isErrorMessage;

    /// <summary>
    /// Gets the supported currency options available for selection.
    /// </summary>
    public IReadOnlyList<CurrencyOption> CurrencyOptions { get; } = new[]
    {
        new CurrencyOption("USD", "USD ($)"),
        new CurrencyOption("MXN", "MXN ($)"),
        new CurrencyOption("EUR", "EUR (€)")
    };

    /// <summary>
    /// Gets the page title displayed in the settings header.
    /// </summary>
    public string Title => "Ajustes del Sistema";

    /// <summary>
    /// Gets the subtitle describing the settings section.
    /// </summary>
    public string Subtitle => "Preferencias de la aplicación de escritorio";

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsViewModel"/> and loads current settings.
    /// </summary>
    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadData();
    }

    /// <summary>
    /// Reloads persisted settings into editable form fields and clears validation/status banners.
    /// </summary>
    public void LoadData()
    {
        _isLoading = true;
        try
        {
            var settings = _settingsService.GetSettings();
            BusinessName = settings.BusinessName;
            SelectedCurrencyCode = settings.CurrencyCode;
            LowStockThresholdText = settings.DefaultLowStockThreshold.ToString(CultureInfo.InvariantCulture);

            BusinessNameError = string.Empty;
            SetPropertyError(nameof(BusinessName), null);
            LowStockThresholdError = string.Empty;
            SetPropertyError(nameof(LowStockThresholdText), null);
            ClearStatus();
        }
        finally
        {
            _isLoading = false;
        }
    }

    partial void OnBusinessNameChanged(string value)
    {
        if (!_isLoading)
            ValidateBusinessName();
    }

    partial void OnLowStockThresholdTextChanged(string value)
    {
        if (!_isLoading)
            ValidateLowStockThreshold();
    }

    /// <summary>
    /// Validates and persists all modified preferences into <c>settings.json</c>.
    /// </summary>
    [RelayCommand]
    public void SaveSettings()
    {
        ClearStatus();
        ValidateBusinessName();
        ValidateLowStockThreshold();

        if (HasErrors || !TryParseNonNegativeInt(LowStockThresholdText, out var threshold))
            return;

        try
        {
            _settingsService.UpdateSettings(BusinessName, SelectedCurrencyCode, threshold);
            SetSuccessStatus("Ajustes guardados correctamente.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            SetErrorStatus(ex.Message);
        }
    }

    private void ValidateBusinessName()
    {
        var error = string.IsNullOrWhiteSpace(BusinessName)
            ? "El nombre del negocio o almacén es obligatorio."
            : string.Empty;

        BusinessNameError = error;
        SetPropertyError(nameof(BusinessName), error);
    }

    private void ValidateLowStockThreshold()
    {
        var error = TryParseNonNegativeInt(LowStockThresholdText, out _)
            ? string.Empty
            : "Ingrese un umbral entero mayor o igual a 0.";

        LowStockThresholdError = error;
        SetPropertyError(nameof(LowStockThresholdText), error);
    }

    private static bool TryParseNonNegativeInt(string? text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value >= 0;
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

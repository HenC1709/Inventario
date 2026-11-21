using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// Display option pairing an ISO currency code with its human-readable label.
/// </summary>
public sealed record CurrencyOption(string Code, string Label);

/// <summary>
/// ViewModel managing persisted application preferences with <see cref="System.ComponentModel.INotifyDataErrorInfo"/> validation
/// and daily exchange-rate currency conversion.
/// </summary>
public partial class SettingsViewModel : ValidatableViewModel
{
    private static readonly CultureInfo NumberCulture = CultureInfo.GetCultureInfo("en-US");

    private readonly ISettingsService _settingsService;
    private readonly IExchangeRateService? _exchangeRateService;
    private bool _isLoading;
    private string _savedCurrencyCode = "USD";
    private CancellationTokenSource? _previewCts;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExchangeRatePreview))]
    private string _exchangeRatePreviewText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [NotifyPropertyChangedFor(nameof(SaveButtonText))]
    private bool _isBusy;

    /// <summary>
    /// Gets the supported currency options available for selection.
    /// </summary>
    public IReadOnlyList<CurrencyOption> CurrencyOptions { get; } = new[]
    {
        new CurrencyOption("USD", "USD ($)"),
        new CurrencyOption("COP", "COP ($)"),
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
    /// Gets a value indicating whether an exchange-rate preview notice is currently visible.
    /// </summary>
    public bool HasExchangeRatePreview => !string.IsNullOrWhiteSpace(ExchangeRatePreviewText);

    /// <summary>
    /// Gets a value indicating whether the form controls are interactive.
    /// </summary>
    public bool IsNotBusy => !IsBusy;

    /// <summary>
    /// Gets the primary save button caption reflecting busy state.
    /// </summary>
    public string SaveButtonText => IsBusy ? "Convirtiendo y guardando..." : "Guardar Cambios";

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsViewModel"/> and loads current settings.
    /// </summary>
    public SettingsViewModel(
        ISettingsService settingsService,
        IExchangeRateService? exchangeRateService = null)
    {
        _settingsService = settingsService;
        _exchangeRateService = exchangeRateService;
        LoadData();
    }

    /// <summary>
    /// Reloads persisted settings into editable form fields and clears validation/status banners.
    /// </summary>
    public void LoadData()
    {
        CancelPreviewRequest();
        _isLoading = true;
        try
        {
            var settings = _settingsService.GetSettings();
            var normalizedCurrency = CurrencyOptions.Any(c => string.Equals(c.Code, settings.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                ? settings.CurrencyCode.ToUpperInvariant()
                : string.Equals(settings.CurrencyCode, "MXN", StringComparison.OrdinalIgnoreCase)
                    ? "COP"
                    : "USD";

            _savedCurrencyCode = normalizedCurrency;
            BusinessName = settings.BusinessName;
            SelectedCurrencyCode = normalizedCurrency;
            LowStockThresholdText = settings.DefaultLowStockThreshold.ToString(CultureInfo.InvariantCulture);

            BusinessNameError = string.Empty;
            SetPropertyError(nameof(BusinessName), null);
            LowStockThresholdError = string.Empty;
            SetPropertyError(nameof(LowStockThresholdText), null);
            ExchangeRatePreviewText = string.Empty;
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

    partial void OnSelectedCurrencyCodeChanged(string value)
    {
        if (_isLoading)
            return;

        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value, _savedCurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            CancelPreviewRequest();
            ExchangeRatePreviewText = string.Empty;
            return;
        }

        _ = RefreshExchangeRatePreviewAsync(_savedCurrencyCode, value.Trim().ToUpperInvariant());
    }

    /// <summary>
    /// Validates and persists all modified preferences into <c>settings.json</c>,
    /// converting catalog prices at the daily exchange rate whenever the currency changed.
    /// </summary>
    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        ClearStatus();
        ValidateBusinessName();
        ValidateLowStockThreshold();

        if (HasErrors || !TryParseNonNegativeInt(LowStockThresholdText, out var threshold))
            return;

        CancelPreviewRequest();
        IsBusy = true;
        try
        {
            var result = await _settingsService.UpdateSettingsAsync(BusinessName, SelectedCurrencyCode, threshold);
            _savedCurrencyCode = result.Settings.CurrencyCode;
            ExchangeRatePreviewText = string.Empty;

            if (result.CurrencyConverted && result.AppliedQuote != null)
            {
                var quote = result.AppliedQuote;
                var rateText = FormatRate(quote.Rate);
                var countText = result.ConvertedProductsCount == 1
                    ? "1 producto"
                    : $"{result.ConvertedProductsCount} productos";

                var message = quote.IsFallback
                    ? $"Ajustes guardados. Sin conexión: se actualizaron {countText} de {quote.FromCurrency} a {quote.ToCurrency} con tasa de referencia (1 {quote.FromCurrency} = {rateText} {quote.ToCurrency})."
                    : $"Ajustes guardados. Se actualizaron {countText} de {quote.FromCurrency} a {quote.ToCurrency} con tasa del día (1 {quote.FromCurrency} = {rateText} {quote.ToCurrency}).";

                SetSuccessStatus(message);
            }
            else
            {
                SetSuccessStatus("Ajustes guardados correctamente.");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            SetErrorStatus(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshExchangeRatePreviewAsync(string fromCurrency, string toCurrency)
    {
        CancelPreviewRequest();

        if (_exchangeRateService == null)
        {
            ExchangeRatePreviewText = $"Al guardar se cambiará la moneda de {fromCurrency} a {toCurrency}.";
            return;
        }

        var cts = new CancellationTokenSource();
        _previewCts = cts;
        ExchangeRatePreviewText = $"Consultando tasa de cambio del día ({fromCurrency} → {toCurrency})...";

        try
        {
            var quote = await _exchangeRateService.GetExchangeRateAsync(fromCurrency, toCurrency, cts.Token);
            if (cts.IsCancellationRequested)
                return;

            var rateText = FormatRate(quote.Rate);
            ExchangeRatePreviewText = quote.IsFallback
                ? $"Sin conexión — tasa de referencia: 1 {quote.FromCurrency} = {rateText} {quote.ToCurrency}. Al guardar se convertirán los precios del inventario."
                : $"Tasa del día ({quote.RateDate:dd/MM/yyyy}): 1 {quote.FromCurrency} = {rateText} {quote.ToCurrency}. Al guardar se convertirán los precios del inventario.";
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer currency selection or save action.
        }
        catch (Exception)
        {
            if (!cts.IsCancellationRequested)
            {
                ExchangeRatePreviewText = $"Al guardar se consultará la tasa del día para convertir precios de {fromCurrency} a {toCurrency}.";
            }
        }
    }

    private void CancelPreviewRequest()
    {
        if (_previewCts == null)
            return;

        _previewCts.Cancel();
        _previewCts.Dispose();
        _previewCts = null;
    }

    private static string FormatRate(decimal rate)
    {
        return rate >= 1m
            ? rate.ToString("#,##0.00", NumberCulture)
            : rate.ToString("0.######", NumberCulture);
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

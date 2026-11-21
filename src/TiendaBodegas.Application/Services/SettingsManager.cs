using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Interfaces;

namespace TiendaBodegas.Application.Services;

/// <summary>
/// Application service enforcing validation rules, currency price conversion, and persistence for <see cref="AppSettings"/>.
/// </summary>
public class SettingsManager : ISettingsService
{
    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD",
        "COP",
        "EUR"
    };

    private readonly ISettingsRepository _settingsRepository;
    private readonly IProductRepository? _productRepository;
    private readonly IExchangeRateService? _exchangeRateService;

    /// <summary>
    /// Raised whenever settings are persisted successfully.
    /// </summary>
    public event Action<AppSettings>? SettingsChanged;

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsManager"/>.
    /// </summary>
    public SettingsManager(
        ISettingsRepository settingsRepository,
        IProductRepository? productRepository = null,
        IExchangeRateService? exchangeRateService = null)
    {
        _settingsRepository = settingsRepository;
        _productRepository = productRepository;
        _exchangeRateService = exchangeRateService;
    }

    /// <summary>
    /// Retrieves the current application settings.
    /// </summary>
    public AppSettings GetSettings() => _settingsRepository.Get();

    /// <summary>
    /// Validates preference inputs, persists them via <see cref="ISettingsRepository"/>, and notifies listeners.
    /// </summary>
    public AppSettings UpdateSettings(string businessName, string currencyCode, int defaultLowStockThreshold)
    {
        ValidateInputs(businessName, currencyCode, defaultLowStockThreshold);

        var settings = _settingsRepository.Get();
        settings.Update(businessName, currencyCode, defaultLowStockThreshold);
        _settingsRepository.Save(settings);

        SettingsChanged?.Invoke(settings);
        return settings;
    }

    /// <summary>
    /// Validates preference inputs, converts all catalog product prices at the daily exchange rate when the currency changes,
    /// persists updates atomically, and notifies subscribed components.
    /// </summary>
    public async Task<SettingsUpdateResult> UpdateSettingsAsync(
        string businessName,
        string currencyCode,
        int defaultLowStockThreshold,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs(businessName, currencyCode, defaultLowStockThreshold);

        var settings = _settingsRepository.Get();
        var previousCurrency = settings.CurrencyCode.Trim().ToUpperInvariant();
        var targetCurrency = currencyCode.Trim().ToUpperInvariant();
        var currencyChanged = !string.Equals(previousCurrency, targetCurrency, StringComparison.OrdinalIgnoreCase);

        ExchangeRateQuote? appliedQuote = null;
        var convertedProductsCount = 0;

        if (currencyChanged && _exchangeRateService != null)
        {
            appliedQuote = await _exchangeRateService.GetExchangeRateAsync(
                previousCurrency,
                targetCurrency,
                cancellationToken);

            if (_productRepository != null)
            {
                var decimalPlaces = string.Equals(targetCurrency, "COP", StringComparison.OrdinalIgnoreCase) ? 0 : 2;
                var products = _productRepository.GetAll().ToList();
                foreach (var product in products)
                {
                    product.ConvertPrices(appliedQuote.Rate, decimalPlaces);
                }

                _productRepository.UpdateRange(products);
                convertedProductsCount = products.Count;
            }
        }

        settings.Update(businessName, targetCurrency, defaultLowStockThreshold);
        _settingsRepository.Save(settings);

        SettingsChanged?.Invoke(settings);
        return new SettingsUpdateResult(
            settings,
            CurrencyConverted: currencyChanged && appliedQuote != null,
            AppliedQuote: appliedQuote,
            ConvertedProductsCount: convertedProductsCount);
    }

    private static void ValidateInputs(string businessName, string currencyCode, int defaultLowStockThreshold)
    {
        if (string.IsNullOrWhiteSpace(businessName))
            throw new ArgumentException("El nombre del negocio o almacén es obligatorio.", nameof(businessName));

        if (string.IsNullOrWhiteSpace(currencyCode) || !SupportedCurrencies.Contains(currencyCode.Trim()))
            throw new ArgumentException("La moneda seleccionada no es válida.", nameof(currencyCode));

        if (defaultLowStockThreshold < 0)
            throw new ArgumentOutOfRangeException(nameof(defaultLowStockThreshold), "El umbral de bajo stock no puede ser negativo.");
    }
}

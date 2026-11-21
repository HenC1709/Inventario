using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Interfaces;

namespace TiendaBodegas.Application.Services;

/// <summary>
/// Application service enforcing validation rules and persistence for <see cref="AppSettings"/>.
/// </summary>
public class SettingsManager : ISettingsService
{
    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD",
        "MXN",
        "EUR"
    };

    private readonly ISettingsRepository _settingsRepository;

    /// <summary>
    /// Raised whenever settings are persisted successfully.
    /// </summary>
    public event Action<AppSettings>? SettingsChanged;

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsManager"/>.
    /// </summary>
    public SettingsManager(ISettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
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
        if (string.IsNullOrWhiteSpace(businessName))
            throw new ArgumentException("El nombre del negocio o almacén es obligatorio.", nameof(businessName));

        if (string.IsNullOrWhiteSpace(currencyCode) || !SupportedCurrencies.Contains(currencyCode.Trim()))
            throw new ArgumentException("La moneda seleccionada no es válida.", nameof(currencyCode));

        if (defaultLowStockThreshold < 0)
            throw new ArgumentOutOfRangeException(nameof(defaultLowStockThreshold), "El umbral de bajo stock no puede ser negativo.");

        var settings = _settingsRepository.Get();
        settings.Update(businessName, currencyCode, defaultLowStockThreshold);
        _settingsRepository.Save(settings);

        SettingsChanged?.Invoke(settings);
        return settings;
    }
}

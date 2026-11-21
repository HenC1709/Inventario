using TiendaBodegas.Domain.Entities;

namespace TiendaBodegas.Application.Interfaces;

/// <summary>
/// Application service contract for reading, validating, and updating persisted system preferences.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Raised whenever application preferences are updated and saved.
    /// </summary>
    event Action<AppSettings>? SettingsChanged;

    /// <summary>
    /// Retrieves the current persisted application settings.
    /// </summary>
    AppSettings GetSettings();

    /// <summary>
    /// Validates, persists, and broadcasts updated application preferences.
    /// </summary>
    AppSettings UpdateSettings(string businessName, string currencyCode, int defaultLowStockThreshold);
}

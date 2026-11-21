using TiendaBodegas.Domain.Entities;

namespace TiendaBodegas.Domain.Interfaces;

/// <summary>
/// Repository contract for retrieving and persisting application settings.
/// </summary>
public interface ISettingsRepository
{
    /// <summary>
    /// Retrieves the current application settings instance.
    /// </summary>
    AppSettings Get();

    /// <summary>
    /// Persists the updated application settings instance.
    /// </summary>
    void Save(AppSettings settings);
}

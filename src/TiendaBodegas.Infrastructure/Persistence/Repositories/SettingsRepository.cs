using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Interfaces;
using TiendaBodegas.Infrastructure.Persistence.Json;

namespace TiendaBodegas.Infrastructure.Persistence.Repositories;

/// <summary>
/// JSON-backed repository implementation for <see cref="AppSettings"/>.
/// </summary>
public class SettingsRepository : ISettingsRepository
{
    private readonly JsonDataContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsRepository"/>.
    /// </summary>
    public SettingsRepository(JsonDataContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns the active <see cref="AppSettings"/> instance held by the data context.
    /// </summary>
    public AppSettings Get()
    {
        return _context.Settings;
    }

    /// <summary>
    /// Updates the data context settings and persists changes atomically to <c>settings.json</c>.
    /// </summary>
    public void Save(AppSettings settings)
    {
        _context.UpdateSettings(settings);
        _context.SaveJson();
    }
}

using System.Text.Json.Serialization;

namespace TiendaBodegas.Domain.Entities;

/// <summary>
/// Persisted application-wide store and inventory configuration preferences.
/// </summary>
public class AppSettings
{
    [JsonInclude]
    [JsonPropertyName("NombreNegocio")]
    public string BusinessName { get; private set; } = "Almacén Central";

    [JsonInclude]
    [JsonPropertyName("MonedaPrincipal")]
    public string CurrencyCode { get; private set; } = "USD";

    [JsonInclude]
    [JsonPropertyName("UmbralBajoStock")]
    public int DefaultLowStockThreshold { get; private set; } = 10;

    /// <summary>
    /// Gets the currency symbol corresponding to <see cref="CurrencyCode"/>.
    /// </summary>
    [JsonIgnore]
    public string CurrencySymbol => string.Equals(CurrencyCode, "EUR", StringComparison.OrdinalIgnoreCase) ? "€" : "$";

    /// <summary>
    /// Parameterless constructor required for JSON deserialization.
    /// </summary>
    public AppSettings() { }

    /// <summary>
    /// Initializes a new <see cref="AppSettings"/> instance with explicit configuration values.
    /// </summary>
    public AppSettings(string businessName, string currencyCode, int defaultLowStockThreshold)
    {
        Update(businessName, currencyCode, defaultLowStockThreshold);
    }

    /// <summary>
    /// Updates the editable preference fields while normalizing whitespace and currency casing.
    /// </summary>
    public void Update(string businessName, string currencyCode, int defaultLowStockThreshold)
    {
        BusinessName = businessName.Trim();
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        DefaultLowStockThreshold = defaultLowStockThreshold;
    }
}

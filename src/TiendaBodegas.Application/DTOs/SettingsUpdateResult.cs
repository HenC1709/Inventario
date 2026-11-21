using TiendaBodegas.Domain.Entities;

namespace TiendaBodegas.Application.DTOs;

/// <summary>
/// Result of persisting application settings, including exchange rate details when product prices were converted.
/// </summary>
public sealed record SettingsUpdateResult(
    AppSettings Settings,
    bool CurrencyConverted,
    ExchangeRateQuote? AppliedQuote = null,
    int ConvertedProductsCount = 0);

namespace TiendaBodegas.Application.DTOs;

/// <summary>
/// Represents a daily currency exchange rate quote between two ISO currency codes.
/// </summary>
public sealed record ExchangeRateQuote(
    string FromCurrency,
    string ToCurrency,
    decimal Rate,
    DateOnly RateDate,
    string Source,
    bool IsFallback = false);

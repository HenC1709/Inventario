using TiendaBodegas.Application.DTOs;

namespace TiendaBodegas.Application.Interfaces;

/// <summary>
/// Service contract for retrieving daily currency exchange rates between supported currencies.
/// </summary>
public interface IExchangeRateService
{
    /// <summary>
    /// Retrieves the daily exchange rate from <paramref name="fromCurrency"/> to <paramref name="toCurrency"/>.
    /// </summary>
    Task<ExchangeRateQuote> GetExchangeRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default);
}

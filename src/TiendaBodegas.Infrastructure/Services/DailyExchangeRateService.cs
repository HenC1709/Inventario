using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Application.Interfaces;

namespace TiendaBodegas.Infrastructure.Services;

/// <summary>
/// Retrieves daily exchange rates from public no-key HTTP endpoints with same-day caching and offline reference fallback.
/// </summary>
public class DailyExchangeRateService : IExchangeRateService
{
    private const string PrimarySourceName = "open.er-api.com";
    private const string SecondarySourceName = "currency-api (jsDelivr)";
    private const string FallbackSourceName = "Tasa de referencia local";

    private static readonly IReadOnlyDictionary<string, decimal> FallbackRatesPerUsd =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = 1m,
            ["COP"] = 4200m,
            ["EUR"] = 0.92m
        };

    private static readonly HttpClient SharedHttpClient = CreateDefaultHttpClient();

    private readonly HttpClient _httpClient;
    private readonly Func<DateOnly> _todayProvider;
    private readonly ConcurrentDictionary<(string From, string To, DateOnly Date), ExchangeRateQuote> _dailyCache = new();

    /// <summary>
    /// Initializes a new instance of <see cref="DailyExchangeRateService"/>.
    /// </summary>
    public DailyExchangeRateService(HttpClient? httpClient = null, Func<DateOnly>? todayProvider = null)
    {
        _httpClient = httpClient ?? SharedHttpClient;
        _todayProvider = todayProvider ?? (() => DateOnly.FromDateTime(DateTime.Today));
    }

    /// <summary>
    /// Retrieves the daily exchange rate from <paramref name="fromCurrency"/> to <paramref name="toCurrency"/>,
    /// using cached same-day quotes when available, querying public daily APIs, and falling back to local reference rates if offline.
    /// </summary>
    public async Task<ExchangeRateQuote> GetExchangeRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fromCurrency))
            throw new ArgumentException("La moneda de origen es obligatoria.", nameof(fromCurrency));

        if (string.IsNullOrWhiteSpace(toCurrency))
            throw new ArgumentException("La moneda de destino es obligatoria.", nameof(toCurrency));

        var from = fromCurrency.Trim().ToUpperInvariant();
        var to = toCurrency.Trim().ToUpperInvariant();
        var today = _todayProvider();

        if (string.Equals(from, to, StringComparison.Ordinal))
            return new ExchangeRateQuote(from, to, 1m, today, "Misma moneda", IsFallback: false);

        var cacheKey = (from, to, today);
        if (_dailyCache.TryGetValue(cacheKey, out var cachedQuote))
            return cachedQuote;

        var liveQuote = await TryFetchFromOpenErApiAsync(from, to, today, cancellationToken)
            ?? await TryFetchFromCurrencyApiAsync(from, to, today, cancellationToken);

        if (liveQuote != null)
        {
            _dailyCache[cacheKey] = liveQuote;
            return liveQuote;
        }

        return BuildFallbackQuote(from, to, today);
    }

    private async Task<ExchangeRateQuote?> TryFetchFromOpenErApiAsync(
        string from,
        string to,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://open.er-api.com/v6/latest/{Uri.EscapeDataString(from)}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (root.TryGetProperty("result", out var resultProp)
                && !string.Equals(resultProp.GetString(), "success", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!root.TryGetProperty("rates", out var ratesProp)
                || !ratesProp.TryGetProperty(to, out var rateProp)
                || !rateProp.TryGetDecimal(out var rate)
                || rate <= 0m)
            {
                return null;
            }

            var rateDate = today;
            if (root.TryGetProperty("time_last_update_utc", out var updateProp)
                && DateTimeOffset.TryParse(
                    updateProp.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var parsedUtc))
            {
                rateDate = DateOnly.FromDateTime(parsedUtc.UtcDateTime);
            }

            return new ExchangeRateQuote(from, to, rate, rateDate, PrimarySourceName, IsFallback: false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;

            return null;
        }
    }

    private async Task<ExchangeRateQuote?> TryFetchFromCurrencyApiAsync(
        string from,
        string to,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        try
        {
            var fromLower = from.ToLowerInvariant();
            var toLower = to.ToLowerInvariant();
            var url = $"https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/{Uri.EscapeDataString(fromLower)}.json";

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (!root.TryGetProperty(fromLower, out var ratesProp)
                || !ratesProp.TryGetProperty(toLower, out var rateProp)
                || !rateProp.TryGetDecimal(out var rate)
                || rate <= 0m)
            {
                return null;
            }

            var rateDate = today;
            if (root.TryGetProperty("date", out var dateProp)
                && DateOnly.TryParseExact(
                    dateProp.GetString(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedDate))
            {
                rateDate = parsedDate;
            }

            return new ExchangeRateQuote(from, to, rate, rateDate, SecondarySourceName, IsFallback: false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;

            return null;
        }
    }

    private static ExchangeRateQuote BuildFallbackQuote(string from, string to, DateOnly today)
    {
        if (!FallbackRatesPerUsd.TryGetValue(from, out var fromPerUsd)
            || !FallbackRatesPerUsd.TryGetValue(to, out var toPerUsd))
        {
            throw new InvalidOperationException(
                $"No se pudo consultar la tasa del día ni existe tasa de referencia para convertir de {from} a {to}.");
        }

        var crossRate = toPerUsd / fromPerUsd;
        return new ExchangeRateQuote(from, to, crossRate, today, FallbackSourceName, IsFallback: true);
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TiendaBodegas/1.0");
        return client;
    }
}

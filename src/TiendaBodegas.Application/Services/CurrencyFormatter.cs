using System.Globalization;

namespace TiendaBodegas.Application.Services;

/// <summary>
/// Shared monetary formatting utility supporting the active store currencies (<c>USD</c>, <c>COP</c>, <c>EUR</c>).
/// </summary>
public static class CurrencyFormatter
{
    private static readonly CultureInfo NumberCulture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>
    /// Formats a decimal monetary amount according to the active ISO currency code or symbol.
    /// </summary>
    /// <param name="amount">Monetary value to format.</param>
    /// <param name="currencyCodeOrSymbol">Active currency code (<c>USD</c>, <c>COP</c>, <c>EUR</c>) or symbol.</param>
    /// <param name="includeCodePrefix">Whether to prepend <c>COP </c> when formatting Colombian pesos.</param>
    public static string Format(decimal amount, string? currencyCodeOrSymbol, bool includeCodePrefix = true)
    {
        var token = (currencyCodeOrSymbol ?? "USD").Trim().ToUpperInvariant();

        if (token is "EUR" or "€")
            return "€" + amount.ToString("#,##0.00", NumberCulture);

        if (token == "COP")
        {
            var hasFraction = amount != Math.Truncate(amount);
            var numberText = amount.ToString(hasFraction ? "#,##0.00" : "#,##0", NumberCulture);
            return includeCodePrefix ? $"COP ${numberText}" : $"${numberText}";
        }

        return "$" + amount.ToString("#,##0.00", NumberCulture);
    }

    /// <summary>
    /// Normalizes a currency code to uppercase, defaulting to <c>USD</c> when null or whitespace.
    /// </summary>
    public static string NormalizeCurrencyCode(string? currencyCode)
    {
        return string.IsNullOrWhiteSpace(currencyCode)
            ? "USD"
            : currencyCode.Trim().ToUpperInvariant();
    }
}

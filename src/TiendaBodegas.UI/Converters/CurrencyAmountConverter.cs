using System.Globalization;
using System.Windows.Data;

namespace TiendaBodegas.UI.Converters;

/// <summary>
/// WPF multi-value converter formatting decimal monetary values according to the active currency code (<c>USD</c>, <c>COP</c>, <c>EUR</c>).
/// </summary>
public sealed class CurrencyAmountConverter : IMultiValueConverter
{
    private static readonly CultureInfo NumberCulture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>
    /// Formats a monetary amount according to the active ISO currency code (or symbol).
    /// </summary>
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
    /// Formats <c>values[0]</c> (decimal amount) using <c>values[1]</c> (currency code or symbol).
    /// </summary>
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length == 0 || values[0] is not decimal amount)
            return string.Empty;

        var currency = values.Length > 1 && values[1] is string s && !string.IsNullOrWhiteSpace(s)
            ? s
            : "USD";

        return Format(amount, currency, includeCodePrefix: true);
    }

    /// <summary>
    /// Not supported (one-way converter).
    /// </summary>
    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
